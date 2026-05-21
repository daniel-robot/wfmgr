using Wfmgr.Engine.Core;

namespace Wfmgr.Engine.Abstractions;

/// <summary>
/// Central service for executing workflow transitions on any workflow subject.
/// </summary>
public interface ITransitionEngine
{
    /// <summary>
    /// Applies a transition to a workflow subject using engine-level abstractions.
    /// </summary>
    Task<TransitionExecutionResult> ApplyTransitionAsync(
        IWorkflowSubject subject,
        string triggerName,
        GateValidationContext context,
        CancellationToken ct = default,
        string? fallbackToStatus = null);

    /// <summary>
    /// Loads the subject by id then applies the transition.
    /// </summary>
    Task<TransitionExecutionResult> ApplyTransitionAsync(
        string subjectId,
        string triggerName,
        GateValidationContext context,
        CancellationToken ct = default,
        string? fallbackToStatus = null);
}
