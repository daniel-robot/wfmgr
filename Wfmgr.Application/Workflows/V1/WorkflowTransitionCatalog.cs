using System.Collections.ObjectModel;
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Domain.Enums;
using Wfmgr.Domain.WorkItems;

namespace Wfmgr.Application.Workflows.V1;

/// <summary>
/// Central catalog of every named workflow transition in the radiotherapy case lifecycle.
/// <para>
/// Transitions are grouped by phase and identified by their <c>TransitionCode</c>.
/// Use <see cref="ByCode"/> for O(1) lookup by code, or iterate <see cref="All"/>
/// for validation, documentation generation, or state-machine bootstrapping.
/// </para>
/// <para>
/// This catalog is intentionally a static definitions store.  Execution logic — gate
/// validation, side-effect dispatch, audit writing — lives in the state-machine service
/// layer and is not contained here.
/// </para>
/// </summary>
public static class WorkflowTransitionCatalog
{
    // ─────────────────────────────────────────────────────────────────────────
    // Phase 1 – Intake & Simulation
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>System automatically schedules the image scan work item immediately after case creation.</summary>
    public static readonly TransitionDefinition SIM_001 = new()
    {
        Code = "SIM-001",
        FromStatuses = [CaseStatus.Submitted],
        ToStatus = CaseStatus.SimScheduled,
        TriggerName = "ScheduleSimulation",
        TriggerType = WorkflowTriggerType.System,
        GateChecks = ["CaseActiveNotCancelled"],
        SuccessActions = ["Audit", "RecordTransitionHistory"],
        FailureActions = ["StayInSubmitted"],
    };

    /// <summary>
    /// System auto-starts simulation immediately after case creation, in flows that
    /// skip the scheduled-appointment step (e.g. daily image scan walk-ins).  Pairs
    /// with the <see cref="WorkItemTypes.DailyImageScan"/> work item created at the
    /// same time on the XVI CT device.
    /// </summary>
    public static readonly TransitionDefinition SIM_001A = new()
    {
        Code = "SIM-001A",
        FromStatuses = [CaseStatus.Submitted],
        ToStatus = CaseStatus.SimInProgress,
        TriggerName = "AutoStartSimulation",
        TriggerType = WorkflowTriggerType.System,
        GateChecks = ["CaseActiveNotCancelled"],
        SuccessActions = ["Audit", "RecordTransitionHistory"],
        FailureActions = ["StayInSubmitted"],
        WorkItemsToCreate = [WorkItemTypes.DailyImageScan],
    };

    /// <summary>Simulation technologist or scheduler books the CT simulation appointment.</summary>
    public static readonly TransitionDefinition SIM_002 = new()
    {
        Code = "SIM-002",
        FromStatuses = [CaseStatus.Submitted],
        ToStatus = CaseStatus.SimScheduled,
        TriggerName = "ScheduleSimulation",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "SimTech/Scheduler",
        GateChecks = ["CaseActiveNotCancelled"],
        SuccessActions = ["SaveScheduleInfo"],
        FailureActions = ["StayInSubmitted"],
        WorkItemsToCreate = [WorkItemTypes.SimulationSchedule],
    };

    /// <summary>Simulation technologist marks that the CT simulation scan has begun.</summary>
    public static readonly TransitionDefinition SIM_003 = new()
    {
        Code = "SIM-003",
        FromStatuses = [CaseStatus.SimScheduled],
        ToStatus = CaseStatus.SimInProgress,
        TriggerName = "StartSimulation",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "SimTech",
        GateChecks = ["SimulationScheduleExists"],
        SuccessActions = ["Audit"],
        FailureActions = ["StayInSimScheduled"],
    };

    /// <summary>Simulation technologist submits the CT simulation record, completing the phase.</summary>
    public static readonly TransitionDefinition SIM_004 = new()
    {
        Code = "SIM-004",
        FromStatuses = [CaseStatus.SimInProgress],
        ToStatus = CaseStatus.SimCompleted,
        TriggerName = "SubmitSimulationRecord",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "SimTech",
        GateChecks = ["SimulationRecordFormValid"],
        SuccessActions = ["SaveForm", "CompleteSimulationRecord"],
        FailureActions = ["StayInSimInProgress"],
        WorkItemsToCreate = [WorkItemTypes.ImageValidation],
    };

    /// <summary>
    /// Simulation technologist completes the <see cref="WorkItemTypes.DailyImageScan"/>
    /// work item on the XVI CT device, advancing the case from
    /// <see cref="CaseStatus.SimInProgress"/> to <see cref="CaseStatus.SimCompleted"/>.
    /// Variant of <see cref="SIM_004"/> for the daily-image-scan flow that does not
    /// require a SimulationRecordForm.
    /// </summary>
    public static readonly TransitionDefinition SIM_004A = new()
    {
        Code = "SIM-004A",
        FromStatuses = [CaseStatus.SimInProgress],
        ToStatus = CaseStatus.SimCompleted,
        TriggerName = "CompleteDailyImageScan",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "SimTech",
        GateChecks = ["CaseActiveNotCancelled"],
        SuccessActions = ["Audit", "RecordTransitionHistory"],
        FailureActions = ["StayInSimInProgress"],
    };

    /// <summary>
    /// Physician or admin cancels the case before treatment has started.
    /// Valid from multiple pre-treatment statuses.
    /// Governed by <see cref="WorkflowSlotCodes.S8ExceptionHandlingPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition SIM_005 = new()
    {
        Code = "SIM-005",
        FromStatuses =
        [
            CaseStatus.Submitted,
            CaseStatus.SimScheduled,
            CaseStatus.SimInProgress,
        ],
        ToStatus = CaseStatus.Cancelled,
        TriggerName = "CancelCase",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physician/Admin",
        GateChecks = ["TreatmentNotStarted"],
        SuccessActions = ["SaveCancellationForm", "CloseOpenTasks"],
        FailureActions = ["RejectCancellation"],
        WorkItemsToCreate = [],
        ConfigSlot = WorkflowSlotCodes.S8ExceptionHandlingPolicy,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 2 – Image Acquisition & Forwarding
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// External imaging system notifies that CT images have been stored in DICOM repository.
    /// Triggered by an external event correlated by case key.
    /// </summary>
    public static readonly TransitionDefinition IMG_001 = new()
    {
        Code = "IMG-001",
        FromStatuses = [CaseStatus.SimCompleted],
        ToStatus = CaseStatus.ImageStored,
        TriggerName = "ReceiveCtImageStoredEvent",
        TriggerType = WorkflowTriggerType.ExternalEvent,
        RequiredRole = null,
        GateChecks = ["CaseResolvedByCorrelationKey", "ImageRefsValid"],
        SuccessActions = ["SaveExternalEvent", "SaveImageRefs", "SaveIntegrationReference"],
        FailureActions = ["MarkEventFailed"],
        WorkItemsToCreate = [WorkItemTypes.ImageValidation],
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 3 – Contouring
    // ─────────────────────────────────────────────────────────────────────────

    // ── Granular contouring sub-phase (Auto → Manual → Ready) ────────────────
    // These transitions split the legacy single-bucket "ContouringInProgress"
    // state into explicit Auto/Manual sub-states so that progress driven by
    // third-party systems (e.g. PvMed) can be observed by the application.
    // The legacy CON-001..CON-005 transitions remain in place for backward
    // compatibility.

    /// <summary>
    /// System automatically dispatches images to the third-party auto-contouring
    /// provider (e.g. PvMed) immediately after image storage and moves the case
    /// into the auto-contouring sub-phase.
    /// </summary>
    public static readonly TransitionDefinition CON_010 = new()
    {
        Code = "CON-010",
        FromStatuses = [CaseStatus.ImageStored],
        ToStatus = CaseStatus.AutoContouringInProgress,
        TriggerName = "StartAutoContouring",
        TriggerType = WorkflowTriggerType.System,
        GateChecks = ["ImageRefsValid"],
        SuccessActions = ["EnqueueSendImagesToContourTool", "Audit"],
        FailureActions = ["StayInImageStored"],
        WorkItemsToCreate = [WorkItemTypes.AutoContourMonitor],
        ConfigSlot = WorkflowSlotCodes.S1ContouringStrategy,
    };

    /// <summary>
    /// Auto-contouring tool reports incremental progress; idempotent self-transition.
    /// </summary>
    public static readonly TransitionDefinition CON_011 = new()
    {
        Code = "CON-011",
        FromStatuses = [CaseStatus.AutoContouringInProgress],
        ToStatus = CaseStatus.AutoContouringInProgress,
        TriggerName = "AutoContourProgressUpdated",
        TriggerType = WorkflowTriggerType.ExternalEvent,
        GateChecks = ["EventIdempotent"],
        SuccessActions = ["UpdateProgress", "Audit"],
        FailureActions = ["IgnoreDuplicate"],
    };

    /// <summary>
    /// Auto-contouring completed successfully. RTStruct references are saved and the
    /// case moves to the AutoContouringCompleted state. Manual contouring is then
    /// always entered (S2 manual phase always runs after auto).
    /// </summary>
    public static readonly TransitionDefinition CON_012 = new()
    {
        Code = "CON-012",
        FromStatuses = [CaseStatus.AutoContouringInProgress],
        ToStatus = CaseStatus.AutoContouringCompleted,
        TriggerName = "AutoContourCompleted",
        TriggerType = WorkflowTriggerType.ExternalEvent,
        GateChecks = ["ContourResultRefsValid"],
        SuccessActions = ["SaveRTStructRefs", "CloseAutoContourMonitor"],
        FailureActions = ["StayInAutoContouring"],
    };

    /// <summary>
    /// Auto-contouring failed. Case skips the AutoContouringCompleted state and goes
    /// directly into manual contouring as a recovery.
    /// </summary>
    public static readonly TransitionDefinition CON_013 = new()
    {
        Code = "CON-013",
        FromStatuses = [CaseStatus.AutoContouringInProgress],
        ToStatus = CaseStatus.ManualContouringInProgress,
        TriggerName = "AutoContourFailed",
        TriggerType = WorkflowTriggerType.ExternalEvent,
        SuccessActions = ["AuditFailure"],
        FailureActions = ["RetryIfConfigured"],
        ConfigSlot = WorkflowSlotCodes.S8ExceptionHandlingPolicy,
    };

    /// <summary>
    /// System auto-starts manual contouring after auto-contouring completes
    /// (manual phase always runs after auto in this workflow).
    /// </summary>
    public static readonly TransitionDefinition CON_014 = new()
    {
        Code = "CON-014",
        FromStatuses = [CaseStatus.AutoContouringCompleted],
        ToStatus = CaseStatus.ManualContouringInProgress,
        TriggerName = "StartManualContouring",
        TriggerType = WorkflowTriggerType.System,
        GateChecks = ["ContourResultRefsValid"],
        SuccessActions = ["Audit"],
        FailureActions = ["StayInAutoContouringCompleted"],
    };

    /// <summary>
    /// Physician / SimTech completes the manual contouring work item.
    /// </summary>
    public static readonly TransitionDefinition CON_015 = new()
    {
        Code = "CON-015",
        FromStatuses = [CaseStatus.ManualContouringInProgress],
        ToStatus = CaseStatus.ManualContouringCompleted,
        TriggerName = "CompleteManualContouring",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physician/SimTech",
        SuccessActions = ["SaveContourRefs", "CloseManualContouringWorkItem"],
        FailureActions = ["StayInManualContouring"],
    };

    /// <summary>
    /// System promotes the case from ManualContouringCompleted to ContoursReady so the
    /// existing review pipeline (REV-*) can take over.
    /// </summary>
    public static readonly TransitionDefinition CON_016 = new()
    {
        Code = "CON-016",
        FromStatuses = [CaseStatus.ManualContouringCompleted],
        ToStatus = CaseStatus.ContoursReady,
        TriggerName = "PromoteContoursReady",
        TriggerType = WorkflowTriggerType.System,
        GateChecks = ["ContourResultRefsValid"],
        SuccessActions = ["Audit"],
        FailureActions = ["StayInManualContouringCompleted"],
    };

    /// <summary>
    /// System auto-promotes the case from ContoursReady to PlanningPending. The
    /// contour-review and rework loop has been removed from the live workflow.
    /// </summary>
    public static readonly TransitionDefinition CON_020 = new()
    {
        Code = "CON-020",
        FromStatuses = [CaseStatus.ContoursReady],
        ToStatus = CaseStatus.PlanningPending,
        TriggerName = "PromotePlanningPending",
        TriggerType = WorkflowTriggerType.System,
        SuccessActions = ["Audit", "DispatchPlanning"],
        WorkItemsToCreate = [WorkItemTypes.PlanAssignment],
    };

    // ── Legacy contouring transitions (retained for backward compatibility) ──

    /// <summary>
    /// Contouring tool reports incremental progress on the auto-contouring job.
    /// This is an idempotent self-transition; duplicate events are safely ignored.
    /// </summary>
    public static readonly TransitionDefinition CON_001 = new()
    {
        Code = "CON-001",
        FromStatuses = [CaseStatus.ContouringInProgress],
        ToStatus = CaseStatus.ContouringInProgress,
        TriggerName = "AutoContourProgressUpdated",
        TriggerType = WorkflowTriggerType.ExternalEvent,
        RequiredRole = null,
        GateChecks = ["EventIdempotent"],
        SuccessActions = ["UpdateProgress", "Audit"],
        FailureActions = ["IgnoreDuplicate"],
        WorkItemsToCreate = [WorkItemTypes.AutoContourMonitor],
    };

    /// <summary>
    /// Auto-contouring has completed; RTStruct references are saved and review tasks created.
    /// Whether a second review is created depends on <see cref="WorkflowSlotCodes.S2ContourReviewPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition CON_002 = new()
    {
        Code = "CON-002",
        FromStatuses = [CaseStatus.ContouringInProgress],
        ToStatus = CaseStatus.ContoursReady,
        TriggerName = "AutoContourCompleted",
        TriggerType = WorkflowTriggerType.ExternalEvent,
        RequiredRole = null,
        GateChecks = ["ContourResultRefsValid"],
        SuccessActions = ["SaveRTStructRefs", "CloseAutoContourMonitor"],
        FailureActions = ["CreateContourRework"],
        WorkItemsToCreate = [WorkItemTypes.ContourReview, WorkItemTypes.ContourSecondReview],
        ConfigSlot = WorkflowSlotCodes.S2ContourReviewPolicy,
    };

    /// <summary>
    /// Auto-contouring failed; case moves to rework-required state for manual resolution.
    /// Exception handling governed by <see cref="WorkflowSlotCodes.S8ExceptionHandlingPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition CON_003 = new()
    {
        Code = "CON-003",
        FromStatuses = [CaseStatus.ContouringInProgress],
        ToStatus = CaseStatus.ContourReworkRequired,
        TriggerName = "AutoContourFailed",
        TriggerType = WorkflowTriggerType.ExternalEvent,
        RequiredRole = null,
        GateChecks = ["EventValid"],
        SuccessActions = ["AuditFailure"],
        FailureActions = ["RetryIfConfigured"],
        WorkItemsToCreate = [WorkItemTypes.ManualContouring],
        ConfigSlot = WorkflowSlotCodes.S8ExceptionHandlingPolicy,
    };

    /// <summary>
    /// Physician or system restarts the auto-contouring job after a rework decision.
    /// Governed by <see cref="WorkflowSlotCodes.S1ContouringStrategy"/>.
    /// </summary>
    public static readonly TransitionDefinition CON_004 = new()
    {
        Code = "CON-004",
        FromStatuses = [CaseStatus.ContourReworkRequired],
        ToStatus = CaseStatus.ContouringInProgress,
        TriggerName = "RestartContouring",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physician/System",
        GateChecks = ["RetryAllowed"],
        SuccessActions = ["CreateOutboxRestartContouring"],
        FailureActions = ["StayInContourRework"],
        WorkItemsToCreate = [WorkItemTypes.AutoContourMonitor],
        ConfigSlot = WorkflowSlotCodes.S1ContouringStrategy,
    };

    /// <summary>
    /// Physician or third-party operator submits manual contour results, bypassing auto-contouring.
    /// Whether a second review is created depends on <see cref="WorkflowSlotCodes.S2ContourReviewPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition CON_005 = new()
    {
        Code = "CON-005",
        FromStatuses = [CaseStatus.ContourReworkRequired],
        ToStatus = CaseStatus.ContoursReady,
        TriggerName = "SubmitManualContourResult",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physician/ThirdPartyOperator",
        GateChecks = ["ManualContourPayloadValid"],
        SuccessActions = ["SaveContourRefs"],
        FailureActions = ["StayInContourRework"],
        WorkItemsToCreate = [WorkItemTypes.ContourReview, WorkItemTypes.ContourSecondReview],
        ConfigSlot = WorkflowSlotCodes.S2ContourReviewPolicy,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 4 – Contour Review
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// System automatically begins contour review once contours are ready.
    /// Governed by <see cref="WorkflowSlotCodes.S2ContourReviewPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition REV_001 = new()
    {
        Code = "REV-001",
        FromStatuses = [CaseStatus.ContoursReady],
        ToStatus = CaseStatus.ContoursUnderReview,
        TriggerName = "StartContourReview",
        TriggerType = WorkflowTriggerType.System,
        RequiredRole = null,
        GateChecks = ["ContourResultExists"],
        SuccessActions = ["CreateReviewTasks"],
        WorkItemsToCreate = [WorkItemTypes.ContourReview, WorkItemTypes.ContourSecondReview],
        ConfigSlot = WorkflowSlotCodes.S2ContourReviewPolicy,
    };

    /// <summary>
    /// Physician or physician approves the contours, advancing the case to planning.
    /// Governed by <see cref="WorkflowSlotCodes.S2ContourReviewPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition REV_002 = new()
    {
        Code = "REV-002",
        FromStatuses = [CaseStatus.ContoursUnderReview],
        ToStatus = CaseStatus.PlanningPending,
        TriggerName = "ApproveContours",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physician",
        GateChecks = ["MinimumApprovalsReached"],
        SuccessActions = ["CompleteReviewTasks"],
        FailureActions = ["RejectTransition"],
        WorkItemsToCreate = [WorkItemTypes.PlanAssignment],
        ConfigSlot = WorkflowSlotCodes.S2ContourReviewPolicy,
    };

    /// <summary>
    /// Physician or physician rejects the contours; a rework work item is created.
    /// Governed by <see cref="WorkflowSlotCodes.S2ContourReviewPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition REV_003 = new()
    {
        Code = "REV-003",
        FromStatuses = [CaseStatus.ContoursUnderReview],
        ToStatus = CaseStatus.ContoursRejected,
        TriggerName = "RejectContours",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physician",
        GateChecks = ["RejectionReasonRequired"],
        SuccessActions = ["SaveReviewForm"],
        FailureActions = ["RejectTransition"],
        WorkItemsToCreate = [WorkItemTypes.ContourRework],
        ConfigSlot = WorkflowSlotCodes.S2ContourReviewPolicy,
    };

    /// <summary>
    /// Physician or system resubmits revised contours for another review cycle.
    /// Governed by <see cref="WorkflowSlotCodes.S8ExceptionHandlingPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition REV_004 = new()
    {
        Code = "REV-004",
        FromStatuses = [CaseStatus.ContoursRejected],
        ToStatus = CaseStatus.ContouringInProgress,
        TriggerName = "ResubmitContours",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physician/System",
        GateChecks = ["RevisedContourExists"],
        SuccessActions = ["ClearStaleReviewTasks"],
        FailureActions = ["StayInContoursRejected"],
        WorkItemsToCreate = [WorkItemTypes.AutoContourMonitor],
        ConfigSlot = WorkflowSlotCodes.S8ExceptionHandlingPolicy,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 5 – Treatment Planning
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scheduler or system assigns a dosimetrist/physicist to create the treatment plan.
    /// Governed by <see cref="WorkflowSlotCodes.S3PlanDispatch"/>.
    /// </summary>
    public static readonly TransitionDefinition PLN_001 = new()
    {
        Code = "PLN-001",
        FromStatuses = [CaseStatus.PlanningPending],
        ToStatus = CaseStatus.PlanningAssigned,
        TriggerName = "AssignPlanner",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Scheduler/System",
        GateChecks = ["AssigneeExists"],
        SuccessActions = ["SavePlannerInfo"],
        FailureActions = ["StayInPlanningPending"],
        WorkItemsToCreate = [WorkItemTypes.PlanDesign],
        ConfigSlot = WorkflowSlotCodes.S3PlanDispatch,
    };

    /// <summary>
    /// Dosimetrist or physicist accepts the assigned plan task and begins work.
    /// Governed by <see cref="WorkflowSlotCodes.S3PlanDispatch"/>.
    /// </summary>
    public static readonly TransitionDefinition PLN_002 = new()
    {
        Code = "PLN-002",
        FromStatuses = [CaseStatus.PlanningAssigned],
        ToStatus = CaseStatus.PlanningInProgress,
        TriggerName = "AcceptPlanTask",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Dosimetrist/Physicist",
        GateChecks = ["TaskAssigned"],
        SuccessActions = ["MarkTaskInProgress"],
        FailureActions = ["StayInPlanningAssigned"],
        ConfigSlot = WorkflowSlotCodes.S3PlanDispatch,
    };

    /// <summary>
    /// Dosimetrist or Monaco system submits the completed treatment plan.
    /// </summary>
    public static readonly TransitionDefinition PLN_003 = new()
    {
        Code = "PLN-003",
        FromStatuses = [CaseStatus.PlanningInProgress],
        ToStatus = CaseStatus.PlanReady,
        TriggerName = "SubmitPlan",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Dosimetrist/Monaco",
        GateChecks = ["PlanPayloadValid"],
        SuccessActions = ["CreatePlanVersion"],
        FailureActions = ["RejectSubmit"],
        WorkItemsToCreate = [WorkItemTypes.PlanEvaluation],
    };

    /// <summary>
    /// System or physicist initiates formal review of the submitted plan.
    /// </summary>
    public static readonly TransitionDefinition PLN_004 = new()
    {
        Code = "PLN-004",
        FromStatuses = [CaseStatus.PlanReady],
        ToStatus = CaseStatus.PlanUnderReview,
        TriggerName = "StartPlanReview",
        TriggerType = WorkflowTriggerType.System,
        RequiredRole = null,
        GateChecks = ["PlanVersionExists"],
        SuccessActions = ["CreateEvaluationTask"],
        WorkItemsToCreate = [WorkItemTypes.PlanEvaluation],
    };

    /// <summary>
    /// Physicist or physician rejects the plan, sending it back for redesign.
    /// </summary>
    public static readonly TransitionDefinition PLN_005 = new()
    {
        Code = "PLN-005",
        FromStatuses = [CaseStatus.PlanUnderReview],
        ToStatus = CaseStatus.PlanningInProgress,
        TriggerName = "RejectPlan",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physicist/Physician",
        GateChecks = ["RejectionReasonRequired"],
        SuccessActions = ["IncrementPlanVersionContext"],
        FailureActions = ["StayInPlanReview"],
        WorkItemsToCreate = [WorkItemTypes.PlanDesign],
    };

    /// <summary>
    /// Physicist or physician approves the plan review, completing the planning phase.
    /// An optional re-review may be required depending on <see cref="WorkflowSlotCodes.S4PlanReReviewPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition PLN_006 = new()
    {
        Code = "PLN-006",
        FromStatuses = [CaseStatus.PlanUnderReview],
        ToStatus = CaseStatus.PlanReviewed,
        TriggerName = "ApprovePlanReview",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physicist/Physician",
        GateChecks = ["EvaluationApproved"],
        SuccessActions = ["Audit"],
        FailureActions = ["RejectTransition"],
        WorkItemsToCreate = [WorkItemTypes.PlanReReview],
        ConfigSlot = WorkflowSlotCodes.S4PlanReReviewPolicy,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 6 – Re-review & Prescription
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// System starts an optional secondary plan review when the policy slot requires it.
    /// Governed by <see cref="WorkflowSlotCodes.S4PlanReReviewPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition RX_001 = new()
    {
        Code = "RX-001",
        FromStatuses = [CaseStatus.PlanReviewed],
        ToStatus = CaseStatus.PlanReReviewOptional,
        TriggerName = "StartPlanReReview",
        TriggerType = WorkflowTriggerType.System,
        RequiredRole = null,
        GateChecks = ["S4ReReviewEnabled"],
        SuccessActions = ["CreateReReviewTask"],
        FailureActions = ["SkipIfDisabled"],
        WorkItemsToCreate = [WorkItemTypes.PlanReReview],
        ConfigSlot = WorkflowSlotCodes.S4PlanReReviewPolicy,
    };
    
    /// <summary>
    /// Chief physician or physicist rejects the re-review, sending the plan back for rework.
    /// Governed by <see cref="WorkflowSlotCodes.S4PlanReReviewPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition RX_004 = new()
    {
        Code = "RX-004",
        FromStatuses = [CaseStatus.PlanReReviewOptional],
        ToStatus = CaseStatus.PlanningInProgress,
        TriggerName = "RejectPlanReReview",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physician/Physicist",
        GateChecks = ["ReasonRequired"],
        SuccessActions = ["PlanBackToRework"],
        FailureActions = ["StayInReReview"],
        WorkItemsToCreate = [WorkItemTypes.PlanDesign],
        ConfigSlot = WorkflowSlotCodes.S4PlanReReviewPolicy,
    };

    /// <summary>
    /// Physicist or QA reviewer approves QA; an optional double-check may follow.
    /// Governed by <see cref="WorkflowSlotCodes.S5PlanDoubleCheck"/>.
    /// </summary>
    public static readonly TransitionDefinition QA_002 = new()
    {
        Code = "QA-002",
        FromStatuses = [CaseStatus.PlanQAInProgress],
        ToStatus = CaseStatus.PlanQAApproved,
        TriggerName = "ApproveQA",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physicist/QAReviewer",
        GateChecks = ["QAFormValid"],
        SuccessActions = ["SaveQAReport"],
        FailureActions = ["RejectTransition"],
        WorkItemsToCreate = [WorkItemTypes.PlanDoubleCheck],
        ConfigSlot = WorkflowSlotCodes.S5PlanDoubleCheck,
    };

    /// <summary>
    /// Physicist or QA reviewer rejects QA; case returns to planning or re-review.
    /// Governed by <see cref="WorkflowSlotCodes.S8ExceptionHandlingPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition QA_003 = new()
    {
        Code = "QA-003",
        FromStatuses = [CaseStatus.PlanQAInProgress],
        ToStatus = CaseStatus.PlanQAFailed,
        TriggerName = "RejectQA",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physicist/QAReviewer",
        GateChecks = ["FailureReasonRequired"],
        SuccessActions = ["SaveQAFailure"],
        FailureActions = ["RejectTransition"],
        WorkItemsToCreate = [WorkItemTypes.PlanDesign],
        ConfigSlot = WorkflowSlotCodes.S8ExceptionHandlingPolicy,
    };

    /// <summary>
    /// Physicist or system decides to rework the plan after a QA failure.
    /// Governed by <see cref="WorkflowSlotCodes.S8ExceptionHandlingPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition QA_004 = new()
    {
        Code = "QA-004",
        FromStatuses = [CaseStatus.PlanQAFailed],
        ToStatus = CaseStatus.PlanningInProgress,
        TriggerName = "ReworkAfterQA",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physicist/System",
        GateChecks = ["ReworkDecisionMade"],
        SuccessActions = ["ReopenPlanningPath"],
        FailureActions = ["StayInQAFailed"],
        WorkItemsToCreate = [WorkItemTypes.PlanDesign],
        ConfigSlot = WorkflowSlotCodes.S8ExceptionHandlingPolicy,
    };

    /// <summary>
    /// System starts an optional independent double-check when the policy slot requires it.
    /// Governed by <see cref="WorkflowSlotCodes.S5PlanDoubleCheck"/>.
    /// </summary>
    public static readonly TransitionDefinition QA_005 = new()
    {
        Code = "QA-005",
        FromStatuses = [CaseStatus.PlanQAApproved],
        ToStatus = CaseStatus.PlanDoubleCheckOptional,
        TriggerName = "StartDoubleCheck",
        TriggerType = WorkflowTriggerType.System,
        RequiredRole = null,
        GateChecks = ["S5DoubleCheckEnabled"],
        SuccessActions = ["CreateDoubleCheckTask"],
        FailureActions = ["SkipIfDisabled"],
        WorkItemsToCreate = [WorkItemTypes.PlanDoubleCheck],
        ConfigSlot = WorkflowSlotCodes.S5PlanDoubleCheck,
    };

    /// <summary>
    /// Senior physicist rejects the double-check, returning the case to planning.
    /// Governed by <see cref="WorkflowSlotCodes.S5PlanDoubleCheck"/>.
    /// </summary>
    public static readonly TransitionDefinition QA_008 = new()
    {
        Code = "QA-008",
        FromStatuses = [CaseStatus.PlanDoubleCheckOptional],
        ToStatus = CaseStatus.PlanningInProgress,
        TriggerName = "RejectDoubleCheck",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physicist",
        GateChecks = ["ReasonRequired"],
        SuccessActions = ["ReopenPlanning"],
        FailureActions = ["StayInDoubleCheck"],
        WorkItemsToCreate = [WorkItemTypes.PlanDesign],
        ConfigSlot = WorkflowSlotCodes.S5PlanDoubleCheck,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Catalog collections
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// All transition definitions in canonical order (grouped by phase, then by code).
    /// </summary>
    public static readonly IReadOnlyList<TransitionDefinition> All = new ReadOnlyCollection<TransitionDefinition>(
    [
        // Intake & Simulation
        SIM_001, SIM_001A, SIM_002, SIM_003, SIM_004, SIM_004A, SIM_005,
        // Image Acquisition
        IMG_001,
        // Contouring (granular sub-phase) — review/rework loop removed
        CON_010, CON_011, CON_012, CON_013, CON_014, CON_015, CON_016, CON_020,
        // Treatment Planning
        PLN_001, PLN_002, PLN_003, PLN_004, PLN_005, PLN_006,
        // Re-review & Prescription
        RX_001, RX_004, 
        // Plan QA & Double-check
        QA_002, QA_003, QA_004, QA_005, QA_008
       
    ]);

    /// <summary>
    /// All transition definitions keyed by <see cref="TransitionDefinition.Code"/> for O(1) lookup.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, TransitionDefinition> ByCode =
        new ReadOnlyDictionary<string, TransitionDefinition>(
            All.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase));
}
