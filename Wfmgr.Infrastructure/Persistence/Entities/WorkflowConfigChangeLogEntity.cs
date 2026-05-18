namespace Wfmgr.Infrastructure.Persistence.Entities;

/// <summary>
/// Append-only audit row capturing every mutation to <see cref="WorkflowProfileEntity"/>
/// or <see cref="WorkflowRuleEntity"/>. Written by <c>WorkflowConfigService</c>.
/// </summary>
public class WorkflowConfigChangeLogEntity
{
    public long ChangeLogId { get; set; }

    /// <summary>"Profile" or "Rule".</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Profile or Rule id of the mutated row.</summary>
    public Guid EntityId { get; set; }

    /// <summary>For rule mutations, the owning profile id; for profile mutations, equals <see cref="EntityId"/>.</summary>
    public Guid ProfileId { get; set; }

    /// <summary>"Create", "Update", "Activate", "Deactivate", "Enable", "Disable".</summary>
    public string Action { get; set; } = string.Empty;

    public string? ActorId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Free-text reason supplied by the caller (e.g. <c>UpdateWorkflowRuleRequest.ChangeReason</c>).</summary>
    public string? ChangeReason { get; set; }

    /// <summary>JSON snapshot of the entity after the mutation (or before for delete-style mutations).</summary>
    public string? SnapshotJson { get; set; }
}
