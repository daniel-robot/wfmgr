namespace Wfmgr.Infrastructure.Persistence.Entities;

/// <summary>
/// DB-backed counterpart to the static workflow vocabulary constants
/// (<c>WorkflowRoles</c>, <c>WorkItemTypes</c>, <c>CaseFormTypes</c>).
/// <para>
/// A single table with a <see cref="Kind"/> discriminator avoids three
/// near-identical schemas. Rows seeded from the in-code constants are marked
/// <see cref="IsSystem"/>=true and may be edited (display name / description /
/// disabled) but cannot be deleted, since runtime code still references their
/// codes by string.
/// </para>
/// </summary>
public class WorkflowVocabularyTermEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// Discriminator: one of the constants in
    /// <c>Wfmgr.Application.Workflows.V1.Vocabulary.WorkflowVocabularyKinds</c>.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Stable code (e.g. "Physician", "DailyImageScan", "PlanQAForm").
    /// Unique per <see cref="Kind"/>.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Optional human-readable label (defaults to <see cref="Code"/> in the UI).</summary>
    public string? DisplayName { get; set; }

    /// <summary>Optional human-readable description.</summary>
    public string? Description { get; set; }

    /// <summary>Sort order within the kind (defaults to seed order, then 1000+ for new rows).</summary>
    public int SortOrder { get; set; }

    /// <summary>True when seeded from in-code constants. System rows cannot be deleted.</summary>
    public bool IsSystem { get; set; }

    /// <summary>False to hide from new transition wiring; existing transitions still resolve by code.</summary>
    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
