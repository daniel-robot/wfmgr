namespace Wfmgr.Domain.Integrations;

/// <summary>
/// Transport over which an outbox message will be delivered to its destination.
/// </summary>
public enum OutboxDeliveryMode
{
    /// <summary>Direct HTTP call to the integration adapter (legacy default).</summary>
    Http = 0,

    /// <summary>Publish to the message broker; consumer routes to the integration.</summary>
    Bus = 1,
}
