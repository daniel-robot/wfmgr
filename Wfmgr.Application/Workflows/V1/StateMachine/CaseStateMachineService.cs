using System.Text.Json;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Domain.Enums;

namespace Wfmgr.Application.Workflows.V1.StateMachine;

public class CaseStateMachineService : ICaseStateMachineService
{
    private static readonly HashSet<CaseStatus> CancelAllowedFromStates =
    [
        CaseStatus.Submitted,
        CaseStatus.SimScheduled,
        CaseStatus.SimInProgress,
        CaseStatus.SimCompleted,
        CaseStatus.ImageStored,
        CaseStatus.ImageForwarding,
        CaseStatus.ContouringInProgress,
        CaseStatus.ContoursReady,
        CaseStatus.ContoursUnderReview,
        CaseStatus.ContoursRejected,
        CaseStatus.ContourReworkRequired,
        CaseStatus.PlanningPending,
        CaseStatus.PlanningAssigned,
        CaseStatus.PlanningInProgress,
        CaseStatus.PlanReady,
        CaseStatus.PlanUnderReview,
        CaseStatus.PlanReviewed,
        CaseStatus.PlanReReviewOptional,
        CaseStatus.PrescriptionGenerating,
        CaseStatus.PrescriptionReady,
        CaseStatus.PrescriptionSyncFailed,
        CaseStatus.PlanQAInProgress,
        CaseStatus.PlanQAApproved,
        CaseStatus.PlanQAFailed,
        CaseStatus.PlanDoubleCheckOptional,
        CaseStatus.ReadyForScheduling,
        CaseStatus.SchedulingInProgress,
        CaseStatus.Scheduled,
        CaseStatus.OrderPending,
        CaseStatus.OrderSubmitted,
        CaseStatus.QueuePending
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
        new() { FromStatus = CaseStatus.Submitted, ToStatus = CaseStatus.SimScheduled, TriggerName = "ScheduleSimulation", TriggerType = WorkflowTriggerType.User },
        new() { FromStatus = CaseStatus.SimScheduled, ToStatus = CaseStatus.SimInProgress, TriggerName = "StartSimulation", TriggerType = WorkflowTriggerType.User },
        new() { FromStatus = CaseStatus.SimInProgress, ToStatus = CaseStatus.SimCompleted, TriggerName = "CompleteSimulation", TriggerType = WorkflowTriggerType.User, GateConditions = ["SimulationRecordMustExist"], FailurePath = "SimRecordMissing" },

        new() { FromStatus = CaseStatus.SimCompleted, ToStatus = CaseStatus.ImageStored, TriggerName = "StoreImage", TriggerType = WorkflowTriggerType.ExternalEvent, GateConditions = ["ImageReferenceMustExist"], FailurePath = "ImageReferenceMissing" },
        new() { FromStatus = CaseStatus.ImageStored, ToStatus = CaseStatus.ImageForwarding, TriggerName = "ForwardImage", TriggerType = WorkflowTriggerType.System },
        new() { FromStatus = CaseStatus.ImageForwarding, ToStatus = CaseStatus.ContouringInProgress, TriggerName = "StartContouring", TriggerType = WorkflowTriggerType.System },

        new() { FromStatus = CaseStatus.ContouringInProgress, ToStatus = CaseStatus.ContoursReady, TriggerName = "ContoursReady", TriggerType = WorkflowTriggerType.System },
        new() { FromStatus = CaseStatus.ContouringInProgress, ToStatus = CaseStatus.ContourReworkRequired, TriggerName = "RequestContourRework", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physician" },
        new() { FromStatus = CaseStatus.ContourReworkRequired, ToStatus = CaseStatus.ContouringInProgress, TriggerName = "RestartContouring", TriggerType = WorkflowTriggerType.System },
        new() { FromStatus = CaseStatus.ContourReworkRequired, ToStatus = CaseStatus.ContoursReady, TriggerName = "ReworkedContoursReady", TriggerType = WorkflowTriggerType.User },

        new() { FromStatus = CaseStatus.ContoursReady, ToStatus = CaseStatus.ContoursUnderReview, TriggerName = "StartContourReview", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physician" },
        new() { FromStatus = CaseStatus.ContoursUnderReview, ToStatus = CaseStatus.PlanningPending, TriggerName = "ApproveContours", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physician", GateConditions = ["ContourReviewApprovalMustExist"], FailurePath = "ContourReviewNotApproved" },
        new() { FromStatus = CaseStatus.ContoursUnderReview, ToStatus = CaseStatus.ContoursRejected, TriggerName = "RejectContours", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physician" },
        new() { FromStatus = CaseStatus.ContoursRejected, ToStatus = CaseStatus.ContouringInProgress, TriggerName = "ReopenContouring", TriggerType = WorkflowTriggerType.System },

        new() { FromStatus = CaseStatus.PlanningPending, ToStatus = CaseStatus.PlanningAssigned, TriggerName = "AssignPlanning", TriggerType = WorkflowTriggerType.User, RequiredRole = "Dosimetrist" },
        new() { FromStatus = CaseStatus.PlanningAssigned, ToStatus = CaseStatus.PlanningInProgress, TriggerName = "StartPlanning", TriggerType = WorkflowTriggerType.User, RequiredRole = "Dosimetrist" },
        new() { FromStatus = CaseStatus.PlanningInProgress, ToStatus = CaseStatus.PlanReady, TriggerName = "PlanReady", TriggerType = WorkflowTriggerType.User, RequiredRole = "Dosimetrist", GateConditions = ["PlanVersionMustExist"], FailurePath = "PlanVersionMissing" },
        new() { FromStatus = CaseStatus.PlanReady, ToStatus = CaseStatus.PlanUnderReview, TriggerName = "StartPlanReview", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physician", GateConditions = ["PlanVersionMustExist"], FailurePath = "PlanVersionMissing" },
        new() { FromStatus = CaseStatus.PlanUnderReview, ToStatus = CaseStatus.PlanningInProgress, TriggerName = "RequestPlanChanges", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physician", SideEffectsAsync = IncrementPlanVersionForReworkAsync },
        new() { FromStatus = CaseStatus.PlanUnderReview, ToStatus = CaseStatus.PlanReviewed, TriggerName = "ApprovePlan", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physician" },

        new() { FromStatus = CaseStatus.PlanReviewed, ToStatus = CaseStatus.PlanReReviewOptional, TriggerName = "RequestPlanRereview", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physician" },
        new() { FromStatus = CaseStatus.PlanReviewed, ToStatus = CaseStatus.PrescriptionGenerating, TriggerName = "GeneratePrescription", TriggerType = WorkflowTriggerType.System },
        new() { FromStatus = CaseStatus.PlanReReviewOptional, ToStatus = CaseStatus.PrescriptionGenerating, TriggerName = "GeneratePrescriptionAfterRereview", TriggerType = WorkflowTriggerType.System },
        new() { FromStatus = CaseStatus.PlanReReviewOptional, ToStatus = CaseStatus.PlanningInProgress, TriggerName = "ReturnToPlanning", TriggerType = WorkflowTriggerType.User, SideEffectsAsync = IncrementPlanVersionForReworkAsync },
        new() { FromStatus = CaseStatus.PrescriptionGenerating, ToStatus = CaseStatus.PrescriptionReady, TriggerName = "PrescriptionReady", TriggerType = WorkflowTriggerType.System },
        new() { FromStatus = CaseStatus.PrescriptionGenerating, ToStatus = CaseStatus.PrescriptionSyncFailed, TriggerName = "PrescriptionSyncFailed", TriggerType = WorkflowTriggerType.ExternalEvent, FailurePath = "ManualPrescriptionSync" },
        new() { FromStatus = CaseStatus.PrescriptionSyncFailed, ToStatus = CaseStatus.PrescriptionGenerating, TriggerName = "RetryPrescriptionSync", TriggerType = WorkflowTriggerType.User },
        new() { FromStatus = CaseStatus.PrescriptionSyncFailed, ToStatus = CaseStatus.PrescriptionReady, TriggerName = "ResolvePrescriptionSync", TriggerType = WorkflowTriggerType.User },

        new() { FromStatus = CaseStatus.PrescriptionReady, ToStatus = CaseStatus.PlanQAInProgress, TriggerName = "StartPlanQa", TriggerType = WorkflowTriggerType.User, GateConditions = ["PrescriptionMustExist"], FailurePath = "PrescriptionMissing" },
        new() { FromStatus = CaseStatus.PlanQAInProgress, ToStatus = CaseStatus.PlanQAApproved, TriggerName = "ApproveQa", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physicist" },
        new() { FromStatus = CaseStatus.PlanQAInProgress, ToStatus = CaseStatus.PlanQAFailed, TriggerName = "FailQa", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physicist" },
        new() { FromStatus = CaseStatus.PlanQAFailed, ToStatus = CaseStatus.PlanningInProgress, TriggerName = "ReturnToPlanningAfterQa", TriggerType = WorkflowTriggerType.System },
        new() { FromStatus = CaseStatus.PlanQAApproved, ToStatus = CaseStatus.PlanDoubleCheckOptional, TriggerName = "RequestPlanDoubleCheck", TriggerType = WorkflowTriggerType.User, RequiredRole = "Physicist" },
        new() { FromStatus = CaseStatus.PlanQAApproved, ToStatus = CaseStatus.ReadyForScheduling, TriggerName = "ReadyForScheduling", TriggerType = WorkflowTriggerType.System, GateConditions = ["QaApprovalMustExist"], FailurePath = "QaApprovalMissing" },
        new() { FromStatus = CaseStatus.PlanDoubleCheckOptional, ToStatus = CaseStatus.ReadyForScheduling, TriggerName = "CompleteDoubleCheck", TriggerType = WorkflowTriggerType.User, GateConditions = ["QaApprovalMustExist"], FailurePath = "QaApprovalMissing" },
        new() { FromStatus = CaseStatus.PlanDoubleCheckOptional, ToStatus = CaseStatus.PlanningInProgress, TriggerName = "DoubleCheckFailed", TriggerType = WorkflowTriggerType.User },

        new() { FromStatus = CaseStatus.ReadyForScheduling, ToStatus = CaseStatus.SchedulingInProgress, TriggerName = "StartScheduling", TriggerType = WorkflowTriggerType.User, RequiredRole = "Scheduler" },
        new() { FromStatus = CaseStatus.SchedulingInProgress, ToStatus = CaseStatus.Scheduled, TriggerName = "CompleteScheduling", TriggerType = WorkflowTriggerType.User, RequiredRole = "Scheduler" },
        new() { FromStatus = CaseStatus.Scheduled, ToStatus = CaseStatus.OrderPending, TriggerName = "PrepareOrder", TriggerType = WorkflowTriggerType.System },
        new() { FromStatus = CaseStatus.OrderPending, ToStatus = CaseStatus.OrderSubmitted, TriggerName = "SubmitOrder", TriggerType = WorkflowTriggerType.User },
        new() { FromStatus = CaseStatus.OrderSubmitted, ToStatus = CaseStatus.QueuePending, TriggerName = "QueueForTreatment", TriggerType = WorkflowTriggerType.System },
        new() { FromStatus = CaseStatus.QueuePending, ToStatus = CaseStatus.Treating, TriggerName = "StartTreatment", TriggerType = WorkflowTriggerType.User, GateConditions = ["OrderMustExistBeforeTreating"], FailurePath = "OrderMissing" },
        new() { FromStatus = CaseStatus.Treating, ToStatus = CaseStatus.TreatmentPaused, TriggerName = "PauseTreatment", TriggerType = WorkflowTriggerType.User },
        new() { FromStatus = CaseStatus.TreatmentPaused, ToStatus = CaseStatus.Treating, TriggerName = "ResumeTreatment", TriggerType = WorkflowTriggerType.User },
        new() { FromStatus = CaseStatus.Treating, ToStatus = CaseStatus.TreatmentInterrupted, TriggerName = "InterruptTreatment", TriggerType = WorkflowTriggerType.ExternalEvent },
        new() { FromStatus = CaseStatus.TreatmentInterrupted, ToStatus = CaseStatus.Treating, TriggerName = "ResumeAfterInterruption", TriggerType = WorkflowTriggerType.User },
        new() { FromStatus = CaseStatus.Treating, ToStatus = CaseStatus.TreatmentCompleted, TriggerName = "CompleteTreatment", TriggerType = WorkflowTriggerType.User },

        new() { FromStatus = CaseStatus.TreatmentCompleted, ToStatus = CaseStatus.PostTreatmentReviewPending, TriggerName = "StartPostTreatmentReview", TriggerType = WorkflowTriggerType.System },
        new() { FromStatus = CaseStatus.PostTreatmentReviewPending, ToStatus = CaseStatus.PostTreatmentReviewed, TriggerName = "CompletePostTreatmentReview", TriggerType = WorkflowTriggerType.User },
        new() { FromStatus = CaseStatus.PostTreatmentReviewed, ToStatus = CaseStatus.Archived, TriggerName = "ArchiveCase", TriggerType = WorkflowTriggerType.User, GateConditions = ["PostTreatmentReviewMustExist"], FailurePath = "PostTreatmentReviewMissing" }
    ];

    private static Task IncrementPlanVersionForReworkAsync(CaseData caseData, TransitionExecutionContext _, CancellationToken _2)
    {
        caseData.CurrentPlanVersionNo = (caseData.CurrentPlanVersionNo ?? 0) + 1;
        return Task.CompletedTask;
    }
}
