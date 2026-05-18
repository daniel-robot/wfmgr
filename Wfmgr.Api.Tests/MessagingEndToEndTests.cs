using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Testcontainers.RabbitMq;
using Wfmgr.Application.Integrations;
using Wfmgr.Application.Integrations.Dtos;
using Wfmgr.Application.Workflows.V1.Outbox;
using Wfmgr.Contracts.Monaco;
using Xunit;

namespace Wfmgr.Api.Tests;

/// <summary>
/// End-to-end Phase 1 smoke test: boots RabbitMQ via Testcontainers, configures the
/// API to use the bus for the Monaco import action, publishes a typed message through
/// the configured <see cref="IOutboxPublisher"/>, and asserts that the in-process
/// MassTransit consumer reached the <see cref="IMonacoAdapter"/>.
/// </summary>
/// <remarks>
/// Requires a running Docker daemon. The collection is skipped automatically when
/// Docker is unavailable so CI without Docker (and local dev without it) does not
/// fail the suite. Run with <c>dotnet test --filter "FullyQualifiedName~MessagingEndToEndTests"</c>.
/// </remarks>
public sealed class MessagingEndToEndTests : IAsyncLifetime
{
    private RabbitMqContainer? _rabbit;
    private bool _dockerAvailable;

    public async Task InitializeAsync()
    {
        try
        {
            _rabbit = new RabbitMqBuilder()
                .WithImage("rabbitmq:3.13-management-alpine")
                .Build();
            await _rabbit.StartAsync();
            _dockerAvailable = true;
        }
        catch
        {
            _dockerAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_rabbit is not null) await _rabbit.DisposeAsync();
    }

    [Fact]
    public async Task BusBackedMonacoImport_RoundTripsThroughRabbitMq()
    {
        if (!_dockerAvailable || _rabbit is null)
        {
            // Soft skip: Docker not present. Don't fail the suite.
            return;
        }

        var monaco = Substitute.For<IMonacoAdapter>();
        var monacoCalled = new TaskCompletionSource<(Guid caseId, string payload)>();
        monaco.SendToMonacoImportAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                monacoCalled.TrySetResult((ci.ArgAt<Guid>(0), ci.ArgAt<string>(1)));
                return Task.CompletedTask;
            });

        var uri = new Uri(_rabbit.GetConnectionString()); // amqp://user:pass@host:port
        var factory = new BusTestFactory(monacoStub: monaco, rabbitUri: uri);
        using var _ = factory.CreateClient(); // boots the host (hosted services start MassTransit)

        using var scope = factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IOutboxPublisher>();
        Assert.True(publisher.IsConfigured, "Publisher should be bus-backed when RabbitMq:Host is set.");

        var caseId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            caseId,
            accessionNumber = "ACC-MT-001",
            ctStudyInstanceUid = "1.2.3.4",
            rtStructSeriesInstanceUid = "1.2.3.5",
        });

        await publisher.PublishAsync(
            messageType: typeof(SendToMonacoImport.V1).FullName!,
            payloadJson: payload,
            traceparent: null,
            ct: CancellationToken.None);

        var completed = await Task.WhenAny(monacoCalled.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.Same(monacoCalled.Task, completed);
        var (receivedCaseId, _) = await monacoCalled.Task;
        Assert.Equal(caseId, receivedCaseId);
    }

    [Fact]
    public async Task InboundEvent_PostedToController_RoutedThroughBusToDispatcher()
    {
        if (!_dockerAvailable || _rabbit is null) return;

        var dispatcher = Substitute.For<IExternalEventDispatcher>();
        var dispatched = new TaskCompletionSource<ExternalIntegrationEventRequest>();
        dispatcher.DispatchAsync(Arg.Any<ExternalIntegrationEventRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                dispatched.TrySetResult(ci.ArgAt<ExternalIntegrationEventRequest>(0));
                return Task.CompletedTask;
            });

        var uri = new Uri(_rabbit.GetConnectionString());
        var factory = new BusTestFactory(monacoStub: null, dispatcherStub: dispatcher, rabbitUri: uri, inboundViaBus: true);
        var client = factory.CreateClient();

        // Make sure the MassTransit bus (and its consumer endpoints) are fully started
        // before publishing — otherwise the inbound message can arrive before the queue
        // bindings exist when the suite runs under CPU contention.
        var busControl = factory.Services.GetRequiredService<IBusControl>();
        await busControl.StartAsync(CancellationToken.None);

        var caseId = Guid.NewGuid();
        var body = new ExternalIntegrationEventRequest
        {
            Source = "PvMed",
            Type = "ContourComplete",
            ExternalId = "ext-evt-" + Guid.NewGuid().ToString("N"),
            CaseId = caseId,
            CaseAccessionNumber = "ACC-INB-001",
            OccurredAt = DateTimeOffset.UtcNow,
        };

        var resp = await client.PostAsJsonAsync("/api/integration/events", body);
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);

        var completed = await Task.WhenAny(dispatched.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.Same(dispatched.Task, completed);
        var received = await dispatched.Task;
        Assert.Equal(body.ExternalId, received.ExternalId);
        Assert.Equal(caseId, received.CaseId);
    }

    private sealed class BusTestFactory : WebApplicationFactory<Program>
    {
        private readonly IMonacoAdapter? _monacoStub;
        private readonly IExternalEventDispatcher? _dispatcherStub;
        private readonly Uri _rabbitUri;
        private readonly bool _inboundViaBus;

        public BusTestFactory(
            IMonacoAdapter? monacoStub,
            Uri rabbitUri,
            IExternalEventDispatcher? dispatcherStub = null,
            bool inboundViaBus = false)
        {
            _monacoStub = monacoStub;
            _dispatcherStub = dispatcherStub;
            _rabbitUri = rabbitUri;
            _inboundViaBus = inboundViaBus;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Authentication:Jwt:Secret", "wfmgr-dev-signing-key-at-least-32-chars!!");
            builder.UseSetting("Authentication:Jwt:Issuer", "wfmgr-dev");
            builder.UseSetting("Authentication:Jwt:Audience", "wfmgr-api");

            // Wire RabbitMQ via configuration; this triggers AddMassTransit() in Infrastructure DI.
            builder.UseSetting("RabbitMq:Host", _rabbitUri.Host);
            builder.UseSetting("RabbitMq:Port", _rabbitUri.Port.ToString());
            builder.UseSetting("RabbitMq:VirtualHost", "/");
            var userinfo = _rabbitUri.UserInfo.Split(':', 2);
            builder.UseSetting("RabbitMq:Username", userinfo[0]);
            builder.UseSetting("RabbitMq:Password", userinfo.Length > 1 ? userinfo[1] : string.Empty);
            builder.UseSetting("Messaging:BusActions:0", "SendToMonacoImport");
            if (_inboundViaBus) builder.UseSetting("Messaging:InboundViaBus", "true");

            builder.ConfigureServices(services =>
            {
                if (_monacoStub is not null)
                {
                    services.RemoveAll<IMonacoAdapter>();
                    services.AddSingleton(_monacoStub);
                }
                if (_dispatcherStub is not null)
                {
                    services.RemoveAll<IExternalEventDispatcher>();
                    services.AddScoped(_ => _dispatcherStub);
                }

                // Strip hosted services we don't need (OutboxWorker would poll the DB).
                // MassTransit's own IHostedService stays in place.
                var outboxWorker = services.SingleOrDefault(d =>
                    d.ImplementationType?.Name == "OutboxWorker");
                if (outboxWorker is not null) services.Remove(outboxWorker);
            });
        }
    }
}

file static class ServiceCollectionRemoveExtensions
{
    public static void RemoveAll<T>(this IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(T)) services.RemoveAt(i);
        }
    }
}
