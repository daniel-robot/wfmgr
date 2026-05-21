using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Application.Workflows.V1.Gates;
using Wfmgr.Domain.Enums;
using EngineAbstractions = Wfmgr.Engine.Abstractions;
using EngineCore = Wfmgr.Engine.Core;

namespace Wfmgr.Application.EngineAdapters;

/// <summary>
/// Implements the engine-level <see cref="EngineAbstractions.IGateValidationService"/> by delegating
/// to the host's domain-specific <see cref="IGateValidationService"/>.
/// Resolves the concrete <c>CaseData</c> from the engine-level subject wrapper.
/// </summary>
internal sealed class EngineGateValidationAdapter : EngineAbstractions.IGateValidationService
{
    private readonly IGateValidationService _inner;

    public EngineGateValidationAdapter(IGateValidationService inner)
    {
        _inner = inner;
    }

    public async Task<EngineCore.GateValidationResult> ValidateAsync(
        EngineAbstractions.IWorkflowSubject subject,
        EngineCore.TransitionDefinition transition,
        EngineCore.GateValidationContext context,
        CancellationToken ct)
    {
        var caseData = subject switch
        {
            CaseWorkflowSubject cws => cws.Data,
            _ => throw new NotSupportedException($"Expected CaseWorkflowSubject, got {subject.GetType().Name}")
        };

        var hostTransition = MapToHostDefinition(transition);
        var hostContext = new GateValidationContext
        {
            UserId = context.UserId,
            Roles = context.Roles,
            Reason = context.Reason,
        };

        var hostResult = await _inner.ValidateAsync(caseData, hostTransition, hostContext, ct);
        return MapToEngineResult(hostResult);
    }

    private static TransitionDefinition MapToHostDefinition(EngineCore.TransitionDefinition engineDef)
    {
        return new TransitionDefinition
        {
            Code = engineDef.Code,
            TriggerName = engineDef.TriggerName,
            TriggerType = Enum.TryParse<WorkflowTriggerType>(engineDef.TriggerType, ignoreCase: true, out var tt)
                ? tt
                : WorkflowTriggerType.System,
            FromStatuses = engineDef.FromStatuses
                .Select(s => Enum.TryParse<CaseStatus>(s, ignoreCase: true, out var st) ? st : CaseStatus.Submitted)
                .ToArray(),
            ToStatus = Enum.TryParse<CaseStatus>(engineDef.ToStatus, ignoreCase: true, out var ts)
                ? ts
                : CaseStatus.Submitted,
            RequiredRoles = engineDef.RequiredRoles,
            GateChecks = engineDef.GateChecks,
            SuccessActions = engineDef.SuccessActions,
            FailureActions = engineDef.FailureActions,
            WorkItemsToCreate = engineDef.WorkItemsToCreate,
            ConfigSlot = engineDef.ConfigSlot,
        };
    }

    private static EngineCore.GateValidationResult MapToEngineResult(GateValidationResult hostResult)
    {
        return hostResult.IsValid
            ? EngineCore.GateValidationResult.Success()
            : EngineCore.GateValidationResult.Failure(hostResult.FailedChecks, hostResult.Messages);
    }
}
