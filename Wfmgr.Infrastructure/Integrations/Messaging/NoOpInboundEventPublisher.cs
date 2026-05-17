using Wfmgr.Application.Workflows.V1.Inbound;
using Wfmgr.Contracts.ExternalEvents;

namespace Wfmgr.Infrastructure.Integrations.Messaging;

/// <summary>
/// Fallback used when no broker is configured. Reports unconfigured so callers can route
/// the request to the in-process dispatcher; calling <see cref="PublishAsync"/> directly
/// throws to prevent silently swallowing messages.
/// </summary>
public sealed class NoOpInboundEventPublisher : IInboundEventPublisher
{
    public bool IsConfigured => false;

    public Task PublishAsync(IngestExternalEvent.V1 message, CancellationToken ct) =>
        throw new InvalidOperationException(
            "InboundEventPublisher.PublishAsync called but no message broker is configured. " +
            "Check IsConfigured before publishing and fall back to in-process dispatch.");
}
