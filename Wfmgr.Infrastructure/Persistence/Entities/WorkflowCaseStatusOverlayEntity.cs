namespace Wfmgr.Infrastructure.Persistence.Entities;

/// <summary>
/// Cosmetic overlay for <c>Wfmgr.Domain.Enums.CaseStatus</c> values.
/// One row per enum value; lazily seeded with defaults on first read.
/// <para>
/// Rows in this table never affect engine behaviour — the enum remains the
/// source of truth for which case statuses are valid. Admins use this overlay
/// to customise per-deployment <see cref="DisplayName"/>, <see cref="Description"/>,
/// <see cref="Color"/>, <see cref="Category"/>, and <see cref="SortOrder"/>.
/// </para>
/// </summary>
public class WorkflowCaseStatusOverlayEntity
{
    /// <summary>Enum name (e.g. "Submitted", "SimScheduled"). Primary key.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Underlying numeric enum value (informational; not used as a key).</summary>
    public int Value { get; set; }

    public string? DisplayName { get; set; }
    public string? Description { get; set; }

    /// <summary>Free-form CSS colour string (e.g. "#1976d2").</summary>
    public string? Color { get; set; }

    /// <summary>Phase grouping for the UI (e.g. "Simulation", "Contouring", "Planning").</summary>
    public string? Category { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
