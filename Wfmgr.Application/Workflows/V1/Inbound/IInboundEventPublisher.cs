using Wfmgr.Contracts.ExternalEvents;

namespace Wfmgr.Application.Workflows.V1.Inbound;

/// <summary>
/// Publishes inbound webhook events onto the message bus, when the bus is configured.
/// Distinct from <see cref="Outbox.IOutboxPublisher"/> because inbound webhooks bypass
/// the outbox table — the inbox table (consumed downstream by
/// <c>IExternalEventDispatcher</c>) is the durability guarantee instead.
/// </summary>
public interface IInboundEventPublisher
{
    /// <summary>
    /// Whether a real broker is wired in. When false, the controller falls back to
    /// in-process dispatch.
    /// </summary>
    bool IsConfigured { get; }

    Task PublishAsync(IngestExternalEvent.V1 message, CancellationToken ct);
}
