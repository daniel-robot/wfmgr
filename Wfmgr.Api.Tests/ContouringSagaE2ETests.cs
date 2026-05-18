using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.RabbitMq;
using Wfmgr.Contracts;
using Wfmgr.Contracts.Contouring;
using Wfmgr.Contracts.ExternalEvents;
using Wfmgr.Contracts.Sagas;
using Wfmgr.Infrastructure.Integrations.Messaging.Sagas;
using Wfmgr.Infrastructure.Persistence;
using Xunit;

namespace Wfmgr.Api.Tests;

/// <summary>
/// Phase 3 saga smoke test: starts RabbitMQ via Testcontainers, boots the API against
/// the local Postgres database, publishes a contour-tool message, asserts the
/// ContouringSaga reaches <c>AwaitingContour</c>, then publishes a translated
/// contour-complete event and asserts the saga reaches <c>AwaitingMonacoAck</c>.
/// </summary>
/// <remarks>
/// Requires Docker (for RabbitMQ) AND a running local Postgres with the
/// <c>AddContouringSagaState</c> migration applied (i.e. <c>docker compose up -d</c>
/// at repo root). When either is missing the test is a soft skip.
/// </remarks>
public sealed class ContouringSagaE2ETests : IAsyncLifetime
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
    public async Task SendImagesToContour_StartsSaga_ThenContourCompletedAdvances()
    {
        if (!_dockerAvailable || _rabbit is null) return;

        var uri = new Uri(_rabbit.GetConnectionString());
        await using var factory = new SagaTestFactory(uri);
        var client = factory.CreateClient();

        // Ensure the bus + saga endpoints are started before publishing.
        var bus = factory.Services.GetRequiredService<IBusControl>();
        await bus.StartAsync(CancellationToken.None);

        // Skip if we can't reach the local Postgres — the saga uses the real DB.
        using (var probeScope = factory.Services.CreateScope())
        {
            var db = probeScope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
            try { await db.Database.OpenConnectionAsync(); db.Database.CloseConnection(); }
            catch { return; }
        }

        var caseId = Guid.NewGuid();
        var envelope = new MessageEnvelope(Guid.NewGuid(), caseId, DateTimeOffset.UtcNow, null);

        using var publishScope = factory.Services.CreateScope();
        var publish = publishScope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        // Publish the upstream contour-tool message; StartContouringSagaRelay translates
        // it into StartContouringSaga.V1, which the saga consumes as its Initial event.
        await publish.Publish(new SendImagesToContourTool.V1(
            envelope,
            caseId,
            "ACC-SAGA-001",
            "CON-010",
            "TestTrigger",
            "test-user",
            null));

        // 1. Wait for saga to land in AwaitingContour.
        var state = await PollForStateAsync(factory, caseId, expected: "AwaitingContour", TimeSpan.FromSeconds(20));
        Assert.NotNull(state);
        Assert.Equal("ACC-SAGA-001", state!.AccessionNumber);

        // 2. Publish a contour-complete external event; translator → ContourCompleted.V1 → saga.
        await publish.Publish(new IngestExternalEvent.V1(
            envelope with { MessageId = Guid.NewGuid() },
            Source: "PvMed",
            Type: "contour.completed",
            ExternalId: "ext-" + Guid.NewGuid().ToString("N"),
            CaseId: caseId,
            CaseAccessionNumber: "ACC-SAGA-001",
            OccurredAt: DateTimeOffset.UtcNow,
            ExternalEntityType: null, ExternalEntityId: null, ExternalStatus: null,
            MetadataJson: null, PayloadJson: null,
            CtStudyInstanceUid: null, CtWadoRsUrl: null,
            RtStructSeriesInstanceUid: "1.2.3.99",
            PlanVersionNo: null, FailureReason: null));

        var advanced = await PollForStateAsync(factory, caseId, expected: "AwaitingMonacoAck", TimeSpan.FromSeconds(20));
        Assert.NotNull(advanced);
        Assert.NotNull(advanced!.ContourCompletedAt);
    }

    private static async Task<ContouringSagaState?> PollForStateAsync(
        WebApplicationFactory<Program> factory, Guid caseId, string expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
            var row = await db.ContouringSagas.AsNoTracking().FirstOrDefaultAsync(x => x.CorrelationId == caseId);
            if (row is not null && string.Equals(row.CurrentState, expected, StringComparison.Ordinal))
            {
                return row;
            }
            await Task.Delay(250);
        }
        return null;
    }

    private sealed class SagaTestFactory : WebApplicationFactory<Program>
    {
        private readonly Uri _rabbitUri;
        public SagaTestFactory(Uri rabbitUri) { _rabbitUri = rabbitUri; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Authentication:Jwt:Secret", "wfmgr-dev-signing-key-at-least-32-chars!!");
            builder.UseSetting("Authentication:Jwt:Issuer", "wfmgr-dev");
            builder.UseSetting("Authentication:Jwt:Audience", "wfmgr-api");

            builder.UseSetting("RabbitMq:Host", _rabbitUri.Host);
            builder.UseSetting("RabbitMq:Port", _rabbitUri.Port.ToString());
            builder.UseSetting("RabbitMq:VirtualHost", "/");
            var userinfo = _rabbitUri.UserInfo.Split(':', 2);
            builder.UseSetting("RabbitMq:Username", userinfo[0]);
            builder.UseSetting("RabbitMq:Password", userinfo.Length > 1 ? userinfo[1] : string.Empty);

            builder.ConfigureServices(services =>
            {
                // Strip OutboxWorker so it doesn't poll while the test runs.
                var outboxWorker = services.SingleOrDefault(d => d.ImplementationType?.Name == "OutboxWorker");
                if (outboxWorker is not null) services.Remove(outboxWorker);
            });
        }
    }
}
