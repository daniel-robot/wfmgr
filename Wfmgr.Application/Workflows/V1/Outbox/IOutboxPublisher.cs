namespace Wfmgr.Application.Workflows.V1.Outbox;

/// <summary>
/// Publishes an outbox message to the configured asynchronous transport (RabbitMQ via
/// MassTransit in Phase 1). Called by <c>OutboxWorker</c> when a message's
/// <c>DeliveryMode</c> is <c>Bus</c>.
/// </summary>
public interface IOutboxPublisher
{
    /// <summary>
    /// Whether a real broker is wired in. When false, <see cref="PublishAsync"/> throws.
    /// Useful for tests and dev environments without a broker.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Resolves the typed contract from <paramref name="messageType"/>, deserializes
    /// <paramref name="payloadJson"/> into it, and publishes it on the bus.
    /// </summary>
    Task PublishAsync(
        string messageType,
        string payloadJson,
        string? traceparent,
        CancellationToken ct);
}
