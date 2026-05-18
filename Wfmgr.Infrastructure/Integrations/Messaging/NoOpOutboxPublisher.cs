using Wfmgr.Application.Workflows.V1.Outbox;

namespace Wfmgr.Infrastructure.Integrations.Messaging;

/// <summary>
/// Fallback publisher used when no broker is configured (tests, dev without RabbitMQ).
/// Reports <c>IsConfigured = false</c>; calling <see cref="PublishAsync"/> throws so a
/// misconfigured bus action does not silently swallow messages.
/// </summary>
public sealed class NoOpOutboxPublisher : IOutboxPublisher
{
    public bool IsConfigured => false;

    public Task PublishAsync(string messageType, string payloadJson, string? traceparent, CancellationToken ct) =>
        throw new InvalidOperationException(
            "Outbox message has DeliveryMode=Bus but no message broker is configured. " +
            "Set the RabbitMq:Host configuration value or move the action back to Http delivery.");
}
