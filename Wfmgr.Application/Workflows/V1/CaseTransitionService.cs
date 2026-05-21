using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Application.EngineAdapters;
using Wfmgr.Application.Workflows.V1.Gates;
using Wfmgr.Domain.Enums;
using EngineAbstractions = Wfmgr.Engine.Abstractions;
using EngineCore = Wfmgr.Engine.Core;

namespace Wfmgr.Application.Workflows.V1;

/// <summary>
/// Host adapter that implements <see cref="ICaseTransitionService"/> by delegating
/// to the engine-level <see cref="EngineAbstractions.ITransitionEngine"/>.
/// Maps host domain types (CaseData, CaseStatus) to/from engine-level string-based types.
/// </summary>
public sealed class CaseTransitionService : ICaseTransitionService
{
    private readonly EngineAbstractions.ITransitionEngine _engine;

    public CaseTransitionService(EngineAbstractions.ITransitionEngine engine)
    {
        _engine = engine;
    }

    public async Task<TransitionExecutionResult> ApplyTransitionAsync(
        StateMachine.IWorkflowSubject subject,
        string triggerName,
        GateValidationContext context,
        CancellationToken ct,
        CaseStatus? fallbackToStatus = null)
    {
        if (subject is not CaseData caseData)
            throw new NotSupportedException(
                $"Only CaseData subjects are supported (got {subject.GetType().Name})");

        return await ApplyTransitionAsync(caseData, triggerName, context, ct, fallbackToStatus);
    }

    public async Task<TransitionExecutionResult> ApplyTransitionAsync(
        Guid caseId,
        string triggerName,
        GateValidationContext context,
        CancellationToken ct,
        CaseStatus? fallbackToStatus = null)
    {
        var engineContext = ToEngineContext(context);
        var engineResult = await _engine.ApplyTransitionAsync(
            caseId.ToString(), triggerName, engineContext, ct,
            fallbackToStatus?.ToString());
        return ToHostResult(engineResult);
    }

    public async Task<TransitionExecutionResult> ApplyTransitionAsync(
        CaseData caseData,
        string triggerName,
        GateValidationContext context,
        CancellationToken ct,
        CaseStatus? fallbackToStatus = null)
    {
        var engineSubject = new CaseWorkflowSubject(caseData);
        var engineContext = ToEngineContext(context);
        var engineResult = await _engine.ApplyTransitionAsync(
            engineSubject, triggerName, engineContext, ct,
            fallbackToStatus?.ToString());

        // Sync engine-mutated status back to the CaseData object
        if (engineResult.IsSuccess && engineResult.ToStatus is not null)
        {
            caseData.CurrentStatus = ParseStatus(engineResult.ToStatus);
        }

        return ToHostResult(engineResult);
    }

    private static EngineCore.GateValidationContext ToEngineContext(GateValidationContext ctx)
    {
        return new EngineCore.GateValidationContext
        {
            UserId = ctx.UserId,
            Roles = ctx.Roles,
            Reason = ctx.Reason,
        };
    }

    private static TransitionExecutionResult ToHostResult(EngineCore.TransitionExecutionResult engineResult)
    {
        if (engineResult.IsSuccess)
        {
            var fromStatus = ParseStatus(engineResult.FromStatus);
            var toStatus = ParseStatus(engineResult.ToStatus!);
            return TransitionExecutionResult.Succeeded(engineResult.TransitionCode, fromStatus, toStatus);
        }

        var hFromStatus = ParseStatus(engineResult.FromStatus);
        return engineResult.FailureReason switch
        {
            EngineCore.TransitionFailureReason.NotFound =>
                TransitionExecutionResult.NotFound(hFromStatus, ExtractTriggerName(engineResult.Messages)),
            EngineCore.TransitionFailureReason.RoleDenied =>
                TransitionExecutionResult.RoleDenied(engineResult.TransitionCode, hFromStatus, []),
            EngineCore.TransitionFailureReason.GateCheckFailed =>
                TransitionExecutionResult.GateCheckFailed(
                    engineResult.TransitionCode, hFromStatus,
                    GateValidationResult.Failure(engineResult.FailedChecks, engineResult.Messages)),
            _ => throw new InvalidOperationException($"Unknown failure reason: {engineResult.FailureReason}")
        };
    }

    private static CaseStatus ParseStatus(string? status) =>
        status is not null && Enum.TryParse<CaseStatus>(status, ignoreCase: true, out var s) ? s : CaseStatus.Submitted;

    private static string ExtractTriggerName(IReadOnlyList<string> messages) =>
        messages.Count > 0 && messages[0] is string m
            ? m.Contains("'") ? m.Split('\'')[1] : "unknown"
            : "unknown";
}
