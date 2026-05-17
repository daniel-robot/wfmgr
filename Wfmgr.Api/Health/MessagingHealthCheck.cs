using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Wfmgr.Application.Workflows.V1.Outbox;
using Wfmgr.Infrastructure.Integrations.Messaging;

namespace Wfmgr.Api.Health;

/// <summary>
/// Reports messaging readiness, surfaced at <c>GET /health/messaging</c>.
/// <para>
/// In <c>http-only</c> mode (no broker configured) the check is always Healthy.
/// In <c>bus</c> mode it additionally scrapes RabbitMQ's management API for
/// <c>*_error</c> queue depth and degrades / fails based on the thresholds in
/// <see cref="RabbitMqOptions"/>. A failed scrape is reported as Degraded (the
/// outbox/publisher still works; only observability is impaired).
/// </para>
/// </summary>
public sealed class MessagingHealthCheck : IHealthCheck
{
    private readonly IOutboxPublisher _publisher;
    private readonly IServiceProvider _services;
    private readonly RabbitMqOptions _rabbit;

    public MessagingHealthCheck(
        IOutboxPublisher publisher,
        IServiceProvider services,
        IOptions<RabbitMqOptions> rabbit)
    {
        _publisher = publisher;
        _services = services;
        _rabbit = rabbit.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["mode"] = _publisher.IsConfigured ? "bus" : "http-only",
            ["publisher"] = _publisher.GetType().Name,
        };

        if (!_publisher.IsConfigured)
        {
            return HealthCheckResult.Healthy("Outbox in HTTP-only mode (no broker configured).", data);
        }

        // Bus mode — probe DLQs.
        var mgmt = _services.GetService<IRabbitMqManagementClient>();
        if (mgmt is null)
        {
            return HealthCheckResult.Healthy("Outbox publisher is bus-backed. Management client not registered; skipping DLQ probe.", data);
        }

        try
        {
            var dlqs = await mgmt.GetDeadLetterQueuesAsync(cancellationToken).ConfigureAwait(false);
            var totalBacklog = 0L;
            foreach (var q in dlqs)
            {
                data[$"dlq.{q.Name}"] = q.Messages;
                totalBacklog += q.Messages;
            }
            data["dlq.total"] = totalBacklog;
            data["dlq.queues"] = dlqs.Count;

            if (_rabbit.DeadLetterUnhealthyThreshold > 0 && totalBacklog >= _rabbit.DeadLetterUnhealthyThreshold)
            {
                return HealthCheckResult.Unhealthy(
                    $"Dead-letter backlog {totalBacklog} >= unhealthy threshold {_rabbit.DeadLetterUnhealthyThreshold}.", data: data);
            }
            if (_rabbit.DeadLetterDegradedThreshold > 0 && totalBacklog >= _rabbit.DeadLetterDegradedThreshold)
            {
                return HealthCheckResult.Degraded(
                    $"Dead-letter backlog {totalBacklog} >= degraded threshold {_rabbit.DeadLetterDegradedThreshold}.", data: data);
            }
            return HealthCheckResult.Healthy("Outbox publisher is bus-backed. DLQs clear.", data);
        }
        catch (Exception ex)
        {
            data["probeError"] = ex.GetType().Name + ": " + ex.Message;
            return HealthCheckResult.Degraded(
                "Outbox publisher is bus-backed, but DLQ probe failed.", ex, data);
        }
    }
}

