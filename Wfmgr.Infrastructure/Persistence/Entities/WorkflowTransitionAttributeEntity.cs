namespace Wfmgr.Infrastructure.Persistence.Entities;

/// <summary>
/// Discriminated child rows for a transition's string-array attributes:
/// required roles, gate checks, success/failure actions, work items to create.
/// </summary>
public class WorkflowTransitionAttributeEntity
{
    public Guid TransitionId { get; set; }

    /// <summary>One of <c>WorkflowTransitionAttributeKinds</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public WorkflowTransitionEntity? Transition { get; set; }
}

public static class WorkflowTransitionAttributeKinds
{
    public const string RequiredRole = nameof(RequiredRole);
    public const string GateCheck = nameof(GateCheck);
    public const string SuccessAction = nameof(SuccessAction);
    public const string FailureAction = nameof(FailureAction);
    public const string WorkItemToCreate = nameof(WorkItemToCreate);
}
