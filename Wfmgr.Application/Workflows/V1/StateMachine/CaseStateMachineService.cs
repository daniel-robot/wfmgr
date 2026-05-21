using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Application.EngineAdapters;
using EngineAbstractions = Wfmgr.Engine.Abstractions;

namespace Wfmgr.Application.Workflows.V1.StateMachine;

/// <summary>
/// Adapter that bridges legacy callers using <see cref="ICaseStateMachineService"/>
/// to the unified engine-level <see cref="EngineAbstractions.ITransitionEngine"/> pipeline.
/// </summary>
public class CaseStateMachineService : ICaseStateMachineService
{
    private readonly EngineAbstractions.ITransitionEngine _engine;

    public CaseStateMachineService(EngineAbstractions.ITransitionEngine engine)
    {
        _engine = engine;
    }

    public async Task ApplyTransitionAsync(
        IWorkflowSubject subject,
        Domain.Enums.CaseStatus toStatus,
        TransitionExecutionContext context,
        CancellationToken ct)
    {
        if (subject is not CaseData caseData)
            throw new NotSupportedException(
                $"Only CaseData subjects are supported (got {subject.GetType().Name})");

        var engineSubject = new CaseWorkflowSubject(caseData);
        var gateCtx = new Wfmgr.Engine.Core.GateValidationContext
        {
            UserId = context.TriggeredBy,
            Roles = context.ActorRoles,
            Reason = context.Reason,
        };
        var result = await _engine.ApplyTransitionAsync(
            engineSubject,
            context.TriggerName,
            gateCtx,
            ct,
            fallbackToStatus: toStatus.ToString());
        result.ThrowIfFailed();
    }
}
