namespace Wfmgr.Infrastructure.Persistence.Entities;

/// <summary>
/// DB-backed counterpart to the static <c>WorkflowTransitionCatalog</c> definitions.
/// Each row represents a single named workflow transition; the runtime catalog
/// service hydrates these into <c>TransitionDefinition</c> objects.
/// </summary>
public class WorkflowTransitionEntity
{
    public Guid Id { get; set; }

    /// <summary>Unique business code (e.g. "SIM-001").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Phase grouping for ordering / UI (e.g. "IntakeSimulation", "Contouring").</summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>Sort order within the phase, preserving canonical catalog ordering.</summary>
    public int SortOrder { get; set; }

    /// <summary>Target <c>CaseStatus</c> stored by name (e.g. "SimScheduled").</summary>
    public string ToStatus { get; set; } = string.Empty;

    /// <summary>Trigger name (e.g. "ScheduleSimulation").</summary>
    public string TriggerName { get; set; } = string.Empty;

    /// <summary>Trigger type stored by name (e.g. "System", "User", "ExternalEvent").</summary>
    public string TriggerType { get; set; } = string.Empty;

    /// <summary>Optional workflow slot code that governs this transition.</summary>
    public string? ConfigSlot { get; set; }

    /// <summary>Optional human-readable description (XML doc text from the seed source).</summary>
    public string? Description { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<WorkflowTransitionFromStatusEntity> FromStatuses { get; set; }
        = new List<WorkflowTransitionFromStatusEntity>();

    public ICollection<WorkflowTransitionAttributeEntity> Attributes { get; set; }
        = new List<WorkflowTransitionAttributeEntity>();
}
