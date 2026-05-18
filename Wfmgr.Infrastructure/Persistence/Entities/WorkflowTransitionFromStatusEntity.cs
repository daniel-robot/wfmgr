namespace Wfmgr.Infrastructure.Persistence.Entities;

/// <summary>
/// One row per (transition, from-status) pair, modelling the
/// <c>TransitionDefinition.FromStatuses</c> array.
/// </summary>
public class WorkflowTransitionFromStatusEntity
{
    public Guid TransitionId { get; set; }

    /// <summary><c>CaseStatus</c> name (e.g. "Submitted").</summary>
    public string FromStatus { get; set; } = string.Empty;

    public WorkflowTransitionEntity? Transition { get; set; }
}
