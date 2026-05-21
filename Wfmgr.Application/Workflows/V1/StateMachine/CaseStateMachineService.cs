using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Application.Workflows.V1.Gates;
using Wfmgr.Domain.Enums;

namespace Wfmgr.Application.Workflows.V1.StateMachine;

/// <summary>
/// Adapter that bridges legacy callers using <see cref="ICaseStateMachineService"/>
/// to the unified <see cref="ICaseTransitionService"/> pipeline.
/// <para>
/// All transition logic — catalog lookup, role check, gate validation
/// (via <see cref="IGateValidationService"/>), audit, history, and side-effects —
/// is owned by <see cref="ICaseTransitionService"/>. This service contributes
/// nothing of its own and exists only so that existing callers (e.g.
/// <c>CaseFormService</c>, <c>ExternalEventDispatcher</c>) continue to compile
/// while new code is migrated to depend on <see cref="ICaseTransitionService"/>
/// directly.
/// </para>
/// </summary>
public class CaseStateMachineService : ICaseStateMachineService
{
    private readonly ICaseTransitionService _transitionService;

    public CaseStateMachineService(ICaseTransitionService transitionService)
    {
        _transitionService = transitionService;
    }

    public async Task ApplyTransitionAsync(
        IWorkflowSubject subject,
        CaseStatus toStatus,
        TransitionExecutionContext context,
        CancellationToken ct)
    {
        var gateContext = GateValidationContext.FromTransitionContext(context);
        var result = await _transitionService.ApplyTransitionAsync(
            subject,
            context.TriggerName,
            gateContext,
            ct,
            fallbackToStatus: toStatus);
        result.ThrowIfFailed();
    }
}
