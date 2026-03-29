namespace Wfmgr.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a single work item within a workflow case.
/// </summary>
public enum WorkItemStatus
{
    /// <summary>Work item has been created and is awaiting action.</summary>
    Pending,
    /// <summary>Work item is actively being worked on.</summary>
    InProgress,
    /// <summary>Work item has been completed successfully.</summary>
    Done,
    /// <summary>Work item was rejected and may require a new work item to be raised.</summary>
    Rejected,
    /// <summary>Work item was cancelled and will not be actioned.</summary>
    Cancelled,
    /// <summary>Work item was intentionally skipped (e.g. not applicable for this case profile).</summary>
    Skipped
}
