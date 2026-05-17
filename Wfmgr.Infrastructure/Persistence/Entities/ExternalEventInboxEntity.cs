namespace Wfmgr.Infrastructure.Persistence.Entities;

/// <summary>
/// Inbox row written before any external-event handling proceeds. The composite primary
/// key <c>(Integration, ExternalEventId)</c> enforces idempotency at the database level
/// — a duplicate insert raises a unique-constraint violation that the dispatcher catches
/// and treats as "already processed".
/// </summary>
public class ExternalEventInboxEntity
{
    /// <summary>Integration / source system, e.g. "PvMed", "Monaco", "MSQ", "CT".</summary>
    public string Integration { get; set; } = string.Empty;

    /// <summary>The external system's unique event id (idempotency key).</summary>
    public string ExternalEventId { get; set; } = string.Empty;

    /// <summary>Strongly-typed message contract name (when produced by a typed consumer).</summary>
    public string? MessageType { get; set; }

    /// <summary>Hash of the inbound payload — diagnostic aid for "same id, different body" cases.</summary>
    public string? PayloadHash { get; set; }

    /// <summary>Wfmgr case id this event was correlated to, if resolved.</summary>
    public Guid? CaseId { get; set; }

    /// <summary>W3C trace-context <c>traceparent</c> at the point of receipt.</summary>
    public string? Traceparent { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
