namespace Wfmgr.Infrastructure.Persistence.Entities;

/// <summary>
/// Append-only audit row for mutations to <see cref="WorkflowVocabularyTermEntity"/>.
/// Mirrors the shape of <see cref="WorkflowTransitionChangeLogEntity"/>.
/// </summary>
public class WorkflowVocabularyChangeLogEntity
{
    public long ChangeLogId { get; set; }

    public Guid TermId { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    /// <summary>"Create", "Update", "Enable", "Disable", "Delete".</summary>
    public string Action { get; set; } = string.Empty;

    public string? ActorId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? ChangeReason { get; set; }

    /// <summary>JSON snapshot of the term after the mutation.</summary>
    public string? SnapshotJson { get; set; }
}
