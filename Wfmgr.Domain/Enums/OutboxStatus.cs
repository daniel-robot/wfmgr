namespace Wfmgr.Domain.Enums;

/// <summary>
/// Represents the delivery status of an outbound integration message in the transactional outbox.
/// </summary>
public enum OutboxStatus
{
    /// <summary>Message has been written to the outbox and not yet picked up for delivery.</summary>
    New,
    /// <summary>Message was delivered successfully to the target system.</summary>
    Sent,
    /// <summary>Message delivery failed and will not be retried further.</summary>
    Failed,
    /// <summary>Message delivery failed and is scheduled for a retry attempt.</summary>
    Retrying
}
