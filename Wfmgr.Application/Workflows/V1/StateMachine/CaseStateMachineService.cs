using System.Text.Json;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Domain.Enums;

namespace Wfmgr.Application.Workflows.V1.StateMachine;

public class CaseStateMachineService : ICaseStateMachineService
{
    private static readonly HashSet<CaseStatus> CancelAllowedFromStates =
    [
        CaseStatus.SimScheduled,
        CaseStatus.SimInProgress,
        CaseStatus.SimCompleted,
        CaseStatus.ImageStored,
        CaseStatus.AutoContouringInProgress,
        CaseStatus.AutoContouringCompleted,
        CaseStatus.ManualContouringInProgress,
        CaseStatus.ManualContouringCompleted,
        CaseStatus.ContouringInProgress,
        CaseStatus.ContoursReady,
        CaseStatus.PlanningPending,
        CaseStatus.PlanningAssigned,
        CaseStatus.PlanningInProgress,
        CaseStatus.PlanReady,
        CaseStatus.PlanUnderReview,
        CaseStatus.PlanReviewed,
        CaseStatus.PlanReReviewOptional,
        CaseStatus.PlanQAInProgress,
        CaseStatus.PlanQAApproved,
        CaseStatus.PlanQAFailed,
        CaseStatus.PlanDoubleCheckOptional
    ];

    private readonly IWorkflowDataAccess _dataAccess;
    private readonly ICaseTransitionGateValidator _gateValidator;

    public CaseStateMachineService(IWorkflowDataAccess dataAccess, ICaseTransitionGateValidator gateValidator)
    {
        _dataAccess = dataAccess;
        _gateValidator = gateValidator;
    }

    public async Task ApplyTransitionAsync(CaseData caseData, CaseStatus toStatus, TransitionExecutionContext context, CancellationToken ct)
    {
        var rule = GetRule(caseData.CurrentStatus, toStatus, context);

        if (!string.IsNullOrWhiteSpace(rule.RequiredRole)
            && !context.ActorRoles.Contains(rule.RequiredRole, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Transition requires role '{rule.RequiredRole}'.");
        }

        await _gateValidator.ValidateAsync(caseData, rule, context, ct);

        if (rule.SideEffectsAsync is not null)
        {
            await rule.SideEffectsAsync(caseData, context, ct);
        }

        var now = DateTimeOffset.UtcNow;
        var fromStatus = caseData.CurrentStatus;
        caseData.CurrentStatus = toStatus;
        caseData.StatusVersion += 1;
        caseData.UpdatedAt = now;

        await _dataAccess.AddAuditLogAsync(new AuditLogData
        {
            AuditId = Guid.NewGuid(),
            CaseId = caseData.CaseId,
            ActorType = context.TriggerType.ToString(),
            ActorId = context.TriggeredBy,
            Action = context.TriggerName,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            SnapshotJson = JsonSerializer.Serialize(new
            {
                rule.GateConditions,
                rule.FailurePath,
                ContextMetadata = context.Metadata
            }),
            CreatedAt = now
        }, ct);

        await _dataAccess.AddCaseTransitionHistoryAsync(new CaseTransitionHistoryData
        {
            TransitionId = Guid.NewGuid(),
            CaseId = caseData.CaseId,
            FromStatus = fromStatus.ToString(),
            ToStatus = toStatus.ToString(),
            TriggerType = context.TriggerType.ToString(),
            TriggerName = context.TriggerName,
            TriggeredBy = context.TriggeredBy,
            Reason = context.Reason,
            MetadataJson = context.Metadata is null ? null : JsonSerializer.Serialize(context.Metadata),
            CreatedAt = now
        }, ct);
    }

    private static TransitionRule GetRule(CaseStatus fromStatus, CaseStatus toStatus, TransitionExecutionContext context)
    {
        if (toStatus == CaseStatus.Cancelled)
        {
            if (!CancelAllowedFromStates.Contains(fromStatus))
            {
                throw new InvalidOperationException($"Transition from '{fromStatus}' to 'Cancelled' is not allowed.");
            }

            return new TransitionRule
            {
                FromStatus = fromStatus,
                ToStatus = toStatus,
                TriggerName = context.TriggerName,
                TriggerType = context.TriggerType,
                GateConditions = ["PreTreatmentCancellationOnly"],
                FailurePath = "RejectTransition"
            };
        }

        return Rules.FirstOrDefault(x => x.FromStatus == fromStatus && x.ToStatus == toStatus)
            ?? throw new InvalidOperationException($"Transition from '{fromStatus}' to '{toStatus}' is not allowed.");
    }

    private static readonly IReadOnlyList<TransitionRule> Rules =
    [
        new() { FromStatus = CaseStatus.Submitted, ToStatus = CaseStatus.SimInProgress, TriggerName = "StartSimulation", TriggerType = WorkflowTriggerType.User },
        new() { FromStatus = CaseStatus.SimInProgress, ToStatus = CaseStatus.SimCompleted, TriggerName = "CompleteSimulation", TriggerType = WorkflowTriggerType.User },

        new() { FromStatus = CaseStatus.SimCompleted, ToStatus = CaseStatus.ImageStored, TriggerName = "StoreImage", TriggerType = WorkflowTriggerType.ExternalEvent, GateConditions = ["ImageReferenceMustExist"], FailurePath = "ImageReferenceMissing" },
        new() { FromStatus = CaseStatus.ImageStored, ToStatus = CaseStatus.ContouringInProgress, TriggerName = "ForwardImage", TriggerType = WorkflowTriggerType.System },

        // ── Granular contouring sub-phase (Auto → Manual → Ready) ────────────
        // These rules pair with WorkflowTransitionCatalog.CON-010..CON-017 and
        // split the legacy single-bucket "ContouringInProgress" state.
        new() { FromStatus = CaseStatus.ImageStored, ToStatus = CaseStatus.AutoContouringInProgress, TriggerName = "StartAutoContouring", TriggerType = WorkflowTriggerType.System },
        new() { FromStatus = CaseStatus.AutoContouringInProgress, ToStatus = CaseStatus.AutoContouringInProgress, TriggerName = "AutoContourProgressUpdated", TriggerType = WorkflowTriggerType.ExternalEvent },
        new() { FromStatus = CaseStatus.AutoContouringInProgress, ToStatus = CaseStatus.AutoContouringCompleted, TriggerName = "AutoContourCompleted", TriggerType = WorkflowTriggerType.ExternalEvent, GateConditions = ["ContourResultRefsValid"], FailurePath = "StayInAutoContouring" },
        new() { FromStatus = CaseStatus.AutoContouringInProgress, ToStatus = CaseStatus.ManualContouringInProgress, TriggerName = "AutoContourFailed", TriggerType = WorkflowTriggerType.ExternalEvent },
        new() { FromStatus = CaseStatus.AutoContouringCompleted, ToStatus = CaseStatus.ManualContouringInProgress, TriggerName = "StartManualContouring", TriggerType = WorkflowTriggerType.System },
        new() { FromStatus = CaseStatus.ManualContouringInProgress, ToStatus = CaseStatus.ManualContouringCompleted, TriggerName = "CompleteManualContouring", TriggerType = WorkflowTriggerType.User },
        new() { FromStatus = CaseStatus.ManualContouringCompleted, ToStatus = CaseStatus.ContoursReady, TriggerName = "PromoteContoursReady", TriggerType = WorkflowTriggerType.System },
        new() { FromStatus = CaseStatus.ContoursReady, ToStatus = CaseStatus.PlanningPending, TriggerName = "PromotePlanningPending", TriggerType = WorkflowTriggerType.System },

        new() { FromStatus = CaseStatus.ContouringInProgress, ToStatus = CaseStatus.ContoursReady, TriggerName = "ContoursReady", TriggerType = WorkflowTriggerType.System },

        new() { FromStatus = CaseStatus.PlanningPending, ToStatus = CaseStatus.PlanningAssigned, TriggerName = "AssignPlanning", TriggerType = WorkflowTriggerType.User, RequiredRole = "Dosimetrist" },
        new() { FromStatus = CaseStatus.PlanningAssigned, ToStatus = CaseStatus.PlanningInProgress, TriggerName = "StartPlanning", TriggerType = WorkflowTriggerType.User, RequiredRole = "Dosimetrist" },
        new() { FromStatus = CaseStatus.PlanningInProgress, ToStatus = CaseStatus.PlanReady, TriggerName = "PlanReady", TriggerType = WorkflowTriggerType.User, RequiredRole = "Dosimetrist", GateConditions = ["PlanVersionMustExist"], FailurePath = "PlanVersionMissing" },
        new() { FromStatus = CaseStatus.PlanReady, ToStatus = CaseStatus.PlanUnderReview, TriggerName = "StartPlanReview", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physician", GateConditions = ["PlanVersionMustExist"], FailurePath = "PlanVersionMissing" },
        new() { FromStatus = CaseStatus.PlanUnderReview, ToStatus = CaseStatus.PlanningInProgress, TriggerName = "RequestPlanChanges", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physician", SideEffectsAsync = IncrementPlanVersionForReworkAsync },
        new() { FromStatus = CaseStatus.PlanUnderReview, ToStatus = CaseStatus.PlanReviewed, TriggerName = "ApprovePlan", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physician" },

        new() { FromStatus = CaseStatus.PlanReviewed, ToStatus = CaseStatus.PlanQAInProgress, TriggerName = "StartPlanQa", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physicist" },
        new() { FromStatus = CaseStatus.PlanQAInProgress, ToStatus = CaseStatus.PlanQAApproved, TriggerName = "ApproveQa", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physicist" },
        new() { FromStatus = CaseStatus.PlanQAInProgress, ToStatus = CaseStatus.PlanQAFailed, TriggerName = "FailQa", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physicist" },
        new() { FromStatus = CaseStatus.PlanQAFailed, ToStatus = CaseStatus.PlanningInProgress, TriggerName = "ReturnToPlanningAfterQa", TriggerType = WorkflowTriggerType.System },
        new() { FromStatus = CaseStatus.PlanQAApproved, ToStatus = CaseStatus.PlanDoubleCheckOptional, TriggerName = "RequestPlanDoubleCheck", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physicist" },
    ];

    private static Task IncrementPlanVersionForReworkAsync(CaseData caseData, TransitionExecutionContext _, CancellationToken _2)
    {
        caseData.CurrentPlanVersionNo = (caseData.CurrentPlanVersionNo ?? 0) + 1;
        return Task.CompletedTask;
    }
}
