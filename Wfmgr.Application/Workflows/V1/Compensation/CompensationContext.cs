namespace Wfmgr.Application.Workflows.V1.Compensation;

/// <summary>
/// Describes the failure that triggered a compensation request.
/// All fields are optional; callers should populate as much detail as available.
/// </summary>
public sealed class CompensationContext
{
    /// <summary>Human-readable description of what went wrong.</summary>
    public string? Reason { get; init; }

    /// <summary>ID of the user who initiated the action that failed (if applicable).</summary>
    public string? UserId { get; init; }

    /// <summary>External system or integration that produced the failure (e.g. "PvMed", "MSQ").</summary>
    public string? SourceSystem { get; init; }

    /// <summary>Raw external event payload that caused or accompanies the failure.</summary>
    public string? ExternalEventPayload { get; init; }

    /// <summary>ID of the outbox message that failed to deliver (if applicable).</summary>
    public Guid? FailedOutboxMessageId { get; init; }

    /// <summary>
    /// Number of retry attempts already made.  Used to decide whether automated
    /// retry should be triggered or the retry limit has been reached.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>Arbitrary key-value metadata for structured logging and audit snapshots.</summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } =
        new Dictionary<string, object?>();
}
