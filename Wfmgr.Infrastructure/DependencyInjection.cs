using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Integrations;
using Wfmgr.Application.Integrations.Dtos;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Config;
using Wfmgr.Application.Workflows.V1.CaseStatuses;
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Application.Workflows.V1.Inbound;
using Wfmgr.Application.Workflows.V1.Outbox;
using Wfmgr.Application.Workflows.V1.Vocabulary;
using Wfmgr.Infrastructure.Integrations;
using Wfmgr.Infrastructure.Integrations.Messaging;
using Wfmgr.Infrastructure.Integrations.Messaging.Consumers;
using Wfmgr.Infrastructure.Integrations.Messaging.Sagas;
using Wfmgr.Infrastructure.Persistence;
using Wfmgr.Infrastructure.Profiles;
using Wfmgr.Infrastructure.Persistence.Repositories;
using Wfmgr.Infrastructure.Workflows;

namespace Wfmgr.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("WfmgrDb")
            ?? throw new InvalidOperationException("Connection string 'WfmgrDb' was not found.");

        services.AddDbContext<WfmgrDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: new[] { "40P01" })));
        services.AddScoped<IWorkflowCaseRepository, WorkflowCaseRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IWorkflowDataAccess, WorkflowDataAccess>();
        services.AddScoped<IWorkflowProfileResolver, WorkflowProfileResolver>();
        services.AddScoped<IWorkflowConfigService, WorkflowConfigService>();
        services.AddSingleton<IWorkflowTransitionCatalogService, WorkflowTransitionCatalogService>();
        services.AddSingleton<IWorkflowVocabularyCatalogService, WorkflowVocabularyCatalogService>();
        services.AddSingleton<ICaseStatusOverlayService, CaseStatusOverlayService>();
        services.AddHostedService<WorkflowSeedingHostedService>();
        services.AddScoped<IExternalEventDispatcher, ExternalEventDispatcher>();
        services.AddHttpClient<IPvMedClient, PvMedClient>(client =>
        {
            var baseUrl = configuration["PvMed:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }
        });
        services.AddHttpClient<IMsqClient, MsqClient>(client =>
        {
            var baseUrl = configuration["Msq:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }
        });
        services.AddScoped<IMonacoAdapter, MonacoAdapter>();

        AddMessaging(services, configuration);

        return services;
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        var rabbitSection = configuration.GetSection(RabbitMqOptions.SectionName);
        services.Configure<RabbitMqOptions>(rabbitSection);
        var rabbit = rabbitSection.Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        if (string.IsNullOrWhiteSpace(rabbit.Host))
        {
            // No broker configured — register a fail-fast publisher. Tests and HTTP-only
            // dev environments take this path and never touch MassTransit.
            services.AddScoped<IOutboxPublisher, NoOpOutboxPublisher>();
            services.AddScoped<IInboundEventPublisher, NoOpInboundEventPublisher>();
            return;
        }

        services.AddMassTransit(cfg =>
        {
            cfg.AddConsumer<SendToMonacoImportConsumer>();
            cfg.AddConsumer<IngestExternalEventConsumer>();
            cfg.AddConsumer<StartContouringSagaRelay>();
            cfg.AddConsumer<SagaExternalEventTranslatorConsumer>();

            // Durable saga state lives in WfmgrDbContext, alongside the outbox + inbox.
            // Optimistic concurrency uses the ISagaVersion token. Postgres lock semantics
            // are sufficient for the modest message rates expected here.
            cfg.AddSagaStateMachine<ContouringSagaStateMachine, ContouringSagaState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                    r.ExistingDbContext<WfmgrDbContext>();
                    r.UsePostgres();
                });

            // NOTE: per-step timeouts (e.g. "escalate if Monaco hasn't acknowledged in
            // 30 min") need a message scheduler. Wiring AddDelayedMessageScheduler() here
            // requires the RabbitMQ delayed_message_exchange plugin in the broker image.
            // Defer this until the production broker has the plugin (or switch to Quartz).

            cfg.UsingRabbitMq((ctx, busCfg) =>
            {
                busCfg.Host(rabbit.Host, rabbit.Port, rabbit.VirtualHost, h =>
                {
                    h.Username(rabbit.Username);
                    h.Password(rabbit.Password);
                });

                // MassTransit retry — at the consumer level — complements the outbox-level
                // retry policy. Both are kept conservative; the outbox is still the source
                // of truth for end-to-end retry exhaustion + compensation escalation.
                busCfg.UseMessageRetry(r => r.Exponential(
                    retryLimit: 3,
                    minInterval: TimeSpan.FromSeconds(2),
                    maxInterval: TimeSpan.FromSeconds(30),
                    intervalDelta: TimeSpan.FromSeconds(5)));

                // Transactional side-effects on consume: any messages a consumer publishes
                // via IPublishEndpoint/ISendEndpointProvider are buffered in memory and
                // released only after the consumer completes successfully. If the consumer
                // throws (or is retried), the queued messages are discarded — preventing
                // partial side-effects like "Monaco import sent, but DB write rolled back".
                // Sufficient for in-process side-effects; for cross-DB+broker atomicity
                // upgrade to UseEntityFrameworkOutbox<WfmgrDbContext>(ctx).
                busCfg.UseInMemoryOutbox(ctx);

                busCfg.ConfigureEndpoints(ctx);
            });
        });

        services.AddScoped<IOutboxPublisher, MassTransitOutboxPublisher>();
        services.AddScoped<IInboundEventPublisher, MassTransitInboundEventPublisher>();
        services.AddHttpClient<IRabbitMqManagementClient, RabbitMqManagementClient>();
    }
}
