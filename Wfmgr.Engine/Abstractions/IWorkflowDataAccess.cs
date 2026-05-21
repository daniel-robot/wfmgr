namespace Wfmgr.Engine.Abstractions;

/// <summary>
/// Engine-level data access abstraction for workflow subject persistence.
/// The host provides the implementation, mapping to its own persistence layer.
/// </summary>
public interface IWorkflowDataAccess
{
    /// <summary>Loads a workflow subject by its identifier.</summary>
    Task<IWorkflowSubject?> GetSubjectAsync(string subjectId, CancellationToken ct);

    /// <summary>Updates the subject's status and version. The host persists the change.</summary>
    Task UpdateSubjectStatusAsync(IWorkflowSubject subject, string newStatus, int newVersion, CancellationToken ct);

    /// <summary>Records an audit log entry for a transition.</summary>
    Task AddAuditLogAsync(EngineAuditLogEntry entry, CancellationToken ct);

    /// <summary>Records a transition history entry.</summary>
    Task AddTransitionHistoryAsync(EngineTransitionHistoryEntry entry, CancellationToken ct);

    /// <summary>Flushes pending changes to the store.</summary>
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Engine-level audit log entry (status-agnostic, uses strings).</summary>
public class EngineAuditLogEntry
{
    public string SubjectId { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? SnapshotJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Engine-level transition history entry (status-agnostic, uses strings).</summary>
public class EngineTransitionHistoryEntry
{
    public string SubjectId { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public string TriggerName { get; set; } = string.Empty;
    public string? TriggeredBy { get; set; }
    public string? Reason { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
