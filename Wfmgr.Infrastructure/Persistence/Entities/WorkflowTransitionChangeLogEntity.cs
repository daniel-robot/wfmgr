namespace Wfmgr.Infrastructure.Persistence.Entities;

/// <summary>
/// Append-only audit row for mutations to <see cref="WorkflowTransitionEntity"/>.
/// Mirrors the shape of <see cref="WorkflowConfigChangeLogEntity"/>.
/// </summary>
public class WorkflowTransitionChangeLogEntity
{
    public long ChangeLogId { get; set; }

    /// <summary>Mutated transition's id.</summary>
    public Guid TransitionId { get; set; }

    /// <summary>Mutated transition's business code (e.g. "SIM-001"). Denormalised for query convenience.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>"Create", "Update", "Enable", "Disable", "Delete".</summary>
    public string Action { get; set; } = string.Empty;

    public string? ActorId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? ChangeReason { get; set; }

    /// <summary>JSON snapshot of the transition (with children) after the mutation.</summary>
    public string? SnapshotJson { get; set; }
}
