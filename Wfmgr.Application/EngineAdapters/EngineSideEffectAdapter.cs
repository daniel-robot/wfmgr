using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Application.Workflows.V1.SideEffects;
using Wfmgr.Domain.Enums;
using EngineAbstractions = Wfmgr.Engine.Abstractions;
using EngineCore = Wfmgr.Engine.Core;

namespace Wfmgr.Application.EngineAdapters;

/// <summary>
/// Implements the engine-level <see cref="EngineAbstractions.ISideEffectService"/> by delegating
/// to the host's <see cref="IWorkflowSideEffectService"/>.
/// </summary>
internal sealed class EngineSideEffectAdapter : EngineAbstractions.ISideEffectService
{
    private readonly IWorkflowSideEffectService _inner;

    public EngineSideEffectAdapter(IWorkflowSideEffectService inner)
    {
        _inner = inner;
    }

    public async Task ExecuteAsync(
        EngineCore.TransitionDefinition transition,
        EngineCore.SideEffectContext context,
        CancellationToken ct)
    {
        var caseData = context.Subject switch
        {
            CaseWorkflowSubject cws => cws.Data,
            _ => throw new NotSupportedException($"Expected CaseWorkflowSubject, got {context.Subject.GetType().Name}")
        };

        var hostTransition = new TransitionDefinition
        {
            Code = transition.Code,
            TriggerName = transition.TriggerName,
            TriggerType = Enum.TryParse<WorkflowTriggerType>(transition.TriggerType, ignoreCase: true, out var tt)
                ? tt
                : WorkflowTriggerType.System,
            FromStatuses = transition.FromStatuses
                .Select(s => Enum.TryParse<CaseStatus>(s, ignoreCase: true, out var st) ? st : CaseStatus.Submitted)
                .ToArray(),
            ToStatus = Enum.TryParse<CaseStatus>(transition.ToStatus, ignoreCase: true, out var ts)
                ? ts
                : CaseStatus.Submitted,
            RequiredRoles = transition.RequiredRoles,
            GateChecks = transition.GateChecks,
            SuccessActions = transition.SuccessActions,
            FailureActions = transition.FailureActions,
            WorkItemsToCreate = transition.WorkItemsToCreate,
            ConfigSlot = transition.ConfigSlot,
        };

        var hostSideEffectContext = new SideEffectContext
        {
            CaseData = caseData,
            ValidationContext = new Workflows.V1.Gates.GateValidationContext
            {
                UserId = context.ValidationContext.UserId,
                Roles = context.ValidationContext.Roles,
                Reason = context.ValidationContext.Reason,
            },
            Now = context.Now,
        };

        await _inner.ExecuteAsync(hostTransition, hostSideEffectContext, ct);
    }
}
