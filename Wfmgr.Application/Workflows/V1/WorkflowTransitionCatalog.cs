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
    /// Doctor or admin cancels the case before treatment has started.
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
        RequiredRole = "Doctor/Admin",
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

    /// <summary>
    /// System automatically forwards stored CT images to the contouring tool.
    /// Behaviour depends on <see cref="WorkflowSlotCodes.S1ContouringStrategy"/>.
    /// </summary>
    public static readonly TransitionDefinition IMG_002 = new()
    {
        Code = "IMG-002",
        FromStatuses = [CaseStatus.ImageStored],
        ToStatus = CaseStatus.ImageForwarding,
        TriggerName = "StartImageForwarding",
        TriggerType = WorkflowTriggerType.System,
        RequiredRole = null,
        GateChecks = ["ImageAccessible"],
        SuccessActions = ["CreateOutboxSendImagesToContourTool"],
        FailureActions = ["RetryOutbox"],
        WorkItemsToCreate = [WorkItemTypes.ImageForwardToContourTool],
        ConfigSlot = WorkflowSlotCodes.S1ContouringStrategy,
    };

    /// <summary>
    /// Contouring tool acknowledges receipt of the images, initiating the contouring job.
    /// Behaviour depends on <see cref="WorkflowSlotCodes.S1ContouringStrategy"/>.
    /// </summary>
    public static readonly TransitionDefinition IMG_003 = new()
    {
        Code = "IMG-003",
        FromStatuses = [CaseStatus.ImageForwarding],
        ToStatus = CaseStatus.ContouringInProgress,
        TriggerName = "ContourToolAccepted",
        TriggerType = WorkflowTriggerType.ExternalEvent,
        RequiredRole = null,
        GateChecks = ["ExternalAcceptOrDeliveryConfirmed"],
        SuccessActions = ["SaveExternalJobRef"],
        FailureActions = ["ManualResend"],
        WorkItemsToCreate = [WorkItemTypes.AutoContourMonitor],
        ConfigSlot = WorkflowSlotCodes.S1ContouringStrategy,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 3 – Contouring
    // ─────────────────────────────────────────────────────────────────────────

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
    /// Doctor or system restarts the auto-contouring job after a rework decision.
    /// Governed by <see cref="WorkflowSlotCodes.S1ContouringStrategy"/>.
    /// </summary>
    public static readonly TransitionDefinition CON_004 = new()
    {
        Code = "CON-004",
        FromStatuses = [CaseStatus.ContourReworkRequired],
        ToStatus = CaseStatus.ContouringInProgress,
        TriggerName = "RestartContouring",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Doctor/System",
        GateChecks = ["RetryAllowed"],
        SuccessActions = ["CreateOutboxRestartContouring"],
        FailureActions = ["StayInContourRework"],
        WorkItemsToCreate = [WorkItemTypes.AutoContourMonitor],
        ConfigSlot = WorkflowSlotCodes.S1ContouringStrategy,
    };

    /// <summary>
    /// Doctor or third-party operator submits manual contour results, bypassing auto-contouring.
    /// Whether a second review is created depends on <see cref="WorkflowSlotCodes.S2ContourReviewPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition CON_005 = new()
    {
        Code = "CON-005",
        FromStatuses = [CaseStatus.ContourReworkRequired],
        ToStatus = CaseStatus.ContoursReady,
        TriggerName = "SubmitManualContourResult",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Doctor/ThirdPartyOperator",
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
    /// Doctor or chief doctor approves the contours, advancing the case to planning.
    /// Governed by <see cref="WorkflowSlotCodes.S2ContourReviewPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition REV_002 = new()
    {
        Code = "REV-002",
        FromStatuses = [CaseStatus.ContoursUnderReview],
        ToStatus = CaseStatus.PlanningPending,
        TriggerName = "ApproveContours",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Doctor/ChiefDoctor",
        GateChecks = ["MinimumApprovalsReached"],
        SuccessActions = ["CompleteReviewTasks"],
        FailureActions = ["RejectTransition"],
        WorkItemsToCreate = [WorkItemTypes.PlanAssignment],
        ConfigSlot = WorkflowSlotCodes.S2ContourReviewPolicy,
    };

    /// <summary>
    /// Doctor or chief doctor rejects the contours; a rework work item is created.
    /// Governed by <see cref="WorkflowSlotCodes.S2ContourReviewPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition REV_003 = new()
    {
        Code = "REV-003",
        FromStatuses = [CaseStatus.ContoursUnderReview],
        ToStatus = CaseStatus.ContoursRejected,
        TriggerName = "RejectContours",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Doctor/ChiefDoctor",
        GateChecks = ["RejectionReasonRequired"],
        SuccessActions = ["SaveReviewForm"],
        FailureActions = ["RejectTransition"],
        WorkItemsToCreate = [WorkItemTypes.ContourRework],
        ConfigSlot = WorkflowSlotCodes.S2ContourReviewPolicy,
    };

    /// <summary>
    /// Doctor or system resubmits revised contours for another review cycle.
    /// Governed by <see cref="WorkflowSlotCodes.S8ExceptionHandlingPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition REV_004 = new()
    {
        Code = "REV-004",
        FromStatuses = [CaseStatus.ContoursRejected],
        ToStatus = CaseStatus.ContouringInProgress,
        TriggerName = "ResubmitContours",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Doctor/System",
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
    /// Physicist or doctor rejects the plan, sending it back for redesign.
    /// </summary>
    public static readonly TransitionDefinition PLN_005 = new()
    {
        Code = "PLN-005",
        FromStatuses = [CaseStatus.PlanUnderReview],
        ToStatus = CaseStatus.PlanningInProgress,
        TriggerName = "RejectPlan",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physicist/Doctor",
        GateChecks = ["RejectionReasonRequired"],
        SuccessActions = ["IncrementPlanVersionContext"],
        FailureActions = ["StayInPlanReview"],
        WorkItemsToCreate = [WorkItemTypes.PlanDesign],
    };

    /// <summary>
    /// Physicist or doctor approves the plan review, completing the planning phase.
    /// An optional re-review may be required depending on <see cref="WorkflowSlotCodes.S4PlanReReviewPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition PLN_006 = new()
    {
        Code = "PLN-006",
        FromStatuses = [CaseStatus.PlanUnderReview],
        ToStatus = CaseStatus.PlanReviewed,
        TriggerName = "ApprovePlanReview",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physicist/Doctor",
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
    /// System initiates prescription generation when no re-review is required.
    /// Governed by <see cref="WorkflowSlotCodes.S4PlanReReviewPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition RX_002 = new()
    {
        Code = "RX-002",
        FromStatuses = [CaseStatus.PlanReviewed],
        ToStatus = CaseStatus.PrescriptionGenerating,
        TriggerName = "StartPrescriptionGeneration",
        TriggerType = WorkflowTriggerType.System,
        RequiredRole = null,
        GateChecks = ["NoReReviewRequired"],
        SuccessActions = ["CreateOutboxGeneratePrescription"],
        FailureActions = ["RetryOrManualSync"],
        WorkItemsToCreate = [WorkItemTypes.PrescriptionSync],
        ConfigSlot = WorkflowSlotCodes.S4PlanReReviewPolicy,
    };

    /// <summary>
    /// Chief doctor or senior physicist approves the re-review, triggering prescription generation.
    /// Governed by <see cref="WorkflowSlotCodes.S4PlanReReviewPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition RX_003 = new()
    {
        Code = "RX-003",
        FromStatuses = [CaseStatus.PlanReReviewOptional],
        ToStatus = CaseStatus.PrescriptionGenerating,
        TriggerName = "ApprovePlanReReview",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "ChiefDoctor/SeniorPhysicist",
        GateChecks = ["ReReviewApproved"],
        SuccessActions = ["CreateOutboxGeneratePrescription"],
        FailureActions = ["RejectTransition"],
        WorkItemsToCreate = [WorkItemTypes.PrescriptionSync],
        ConfigSlot = WorkflowSlotCodes.S4PlanReReviewPolicy,
    };

    /// <summary>
    /// Chief doctor or senior physicist rejects the re-review, sending the plan back for rework.
    /// Governed by <see cref="WorkflowSlotCodes.S4PlanReReviewPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition RX_004 = new()
    {
        Code = "RX-004",
        FromStatuses = [CaseStatus.PlanReReviewOptional],
        ToStatus = CaseStatus.PlanningInProgress,
        TriggerName = "RejectPlanReReview",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "ChiefDoctor/SeniorPhysicist",
        GateChecks = ["ReasonRequired"],
        SuccessActions = ["PlanBackToRework"],
        FailureActions = ["StayInReReview"],
        WorkItemsToCreate = [WorkItemTypes.PlanDesign],
        ConfigSlot = WorkflowSlotCodes.S4PlanReReviewPolicy,
    };

    /// <summary>
    /// External system notifies that prescription generation has succeeded.
    /// </summary>
    public static readonly TransitionDefinition RX_005 = new()
    {
        Code = "RX-005",
        FromStatuses = [CaseStatus.PrescriptionGenerating],
        ToStatus = CaseStatus.PrescriptionReady,
        TriggerName = "PrescriptionGenerated",
        TriggerType = WorkflowTriggerType.ExternalEvent,
        RequiredRole = null,
        GateChecks = ["PrescriptionReferenceValid"],
        SuccessActions = ["SaveIntegrationReference"],
        FailureActions = ["RetryOrManualSync"],
        WorkItemsToCreate = [WorkItemTypes.PlanQA],
    };

    /// <summary>
    /// External system reports that prescription synchronisation failed.
    /// Governed by <see cref="WorkflowSlotCodes.S8ExceptionHandlingPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition RX_006 = new()
    {
        Code = "RX-006",
        FromStatuses = [CaseStatus.PrescriptionGenerating],
        ToStatus = CaseStatus.PrescriptionSyncFailed,
        TriggerName = "PrescriptionSyncFailed",
        TriggerType = WorkflowTriggerType.ExternalEvent,
        RequiredRole = null,
        GateChecks = ["FailureEventValid"],
        SuccessActions = ["AuditFailure"],
        FailureActions = ["RetryLater"],
        WorkItemsToCreate = [WorkItemTypes.PrescriptionSync],
        ConfigSlot = WorkflowSlotCodes.S8ExceptionHandlingPolicy,
    };

    /// <summary>
    /// Physicist or system retries the failed prescription synchronisation.
    /// Governed by <see cref="WorkflowSlotCodes.S8ExceptionHandlingPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition RX_007 = new()
    {
        Code = "RX-007",
        FromStatuses = [CaseStatus.PrescriptionSyncFailed],
        ToStatus = CaseStatus.PrescriptionGenerating,
        TriggerName = "RetryPrescriptionSync",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Physicist/System",
        GateChecks = ["RetryAllowed"],
        SuccessActions = ["CreateOutboxPrescriptionSync"],
        FailureActions = ["StayInSyncFailed"],
        WorkItemsToCreate = [WorkItemTypes.PrescriptionSync],
        ConfigSlot = WorkflowSlotCodes.S8ExceptionHandlingPolicy,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 7 – Plan QA & Double-check
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// System automatically starts physics QA once plan and prescription are ready.
    /// </summary>
    public static readonly TransitionDefinition QA_001 = new()
    {
        Code = "QA-001",
        FromStatuses = [CaseStatus.PrescriptionReady],
        ToStatus = CaseStatus.PlanQAInProgress,
        TriggerName = "StartQA",
        TriggerType = WorkflowTriggerType.System,
        RequiredRole = null,
        GateChecks = ["PlanAndPrescriptionPresent"],
        SuccessActions = ["CreateQATask"],
        WorkItemsToCreate = [WorkItemTypes.PlanQA],
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
    /// System skips the double-check when the policy slot has it disabled.
    /// Governed by <see cref="WorkflowSlotCodes.S5PlanDoubleCheck"/>.
    /// </summary>
    public static readonly TransitionDefinition QA_006 = new()
    {
        Code = "QA-006",
        FromStatuses = [CaseStatus.PlanQAApproved],
        ToStatus = CaseStatus.ReadyForScheduling,
        TriggerName = "SkipDoubleCheck",
        TriggerType = WorkflowTriggerType.System,
        RequiredRole = null,
        GateChecks = ["S5DoubleCheckDisabled"],
        SuccessActions = ["Audit"],
        WorkItemsToCreate = [WorkItemTypes.ScheduleSync],
        ConfigSlot = WorkflowSlotCodes.S5PlanDoubleCheck,
    };

    /// <summary>
    /// Senior physicist approves the independent double-check of the plan.
    /// Governed by <see cref="WorkflowSlotCodes.S5PlanDoubleCheck"/>.
    /// </summary>
    public static readonly TransitionDefinition QA_007 = new()
    {
        Code = "QA-007",
        FromStatuses = [CaseStatus.PlanDoubleCheckOptional],
        ToStatus = CaseStatus.ReadyForScheduling,
        TriggerName = "ApproveDoubleCheck",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "SeniorPhysicist",
        GateChecks = ["DoubleCheckApproved"],
        SuccessActions = ["CompleteDoubleCheckTask"],
        FailureActions = ["RejectTransition"],
        WorkItemsToCreate = [WorkItemTypes.ScheduleSync],
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
        RequiredRole = "SeniorPhysicist",
        GateChecks = ["ReasonRequired"],
        SuccessActions = ["ReopenPlanning"],
        FailureActions = ["StayInDoubleCheck"],
        WorkItemsToCreate = [WorkItemTypes.PlanDesign],
        ConfigSlot = WorkflowSlotCodes.S5PlanDoubleCheck,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 8 – Scheduling, Order & Treatment
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// System initiates MSQ schedule synchronisation.
    /// Governed by <see cref="WorkflowSlotCodes.S6QueueAndCancelPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition TRT_001 = new()
    {
        Code = "TRT-001",
        FromStatuses = [CaseStatus.ReadyForScheduling],
        ToStatus = CaseStatus.SchedulingInProgress,
        TriggerName = "StartScheduleSync",
        TriggerType = WorkflowTriggerType.System,
        RequiredRole = null,
        GateChecks = ["CaseReleasedForSchedule"],
        SuccessActions = ["StartScheduleWatch"],
        FailureActions = ["RetryOrManual"],
        WorkItemsToCreate = [WorkItemTypes.ScheduleSync],
        ConfigSlot = WorkflowSlotCodes.S6QueueAndCancelPolicy,
    };

    /// <summary>
    /// External scheduling system confirms that the schedule has been synchronised.
    /// Governed by <see cref="WorkflowSlotCodes.S6QueueAndCancelPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition TRT_002 = new()
    {
        Code = "TRT-002",
        FromStatuses = [CaseStatus.SchedulingInProgress],
        ToStatus = CaseStatus.Scheduled,
        TriggerName = "ScheduleSynced",
        TriggerType = WorkflowTriggerType.ExternalEvent,
        RequiredRole = null,
        GateChecks = ["SchedulePayloadValid"],
        SuccessActions = ["SaveScheduleRef"],
        FailureActions = ["RetryOrManualSync"],
        WorkItemsToCreate = [WorkItemTypes.TreatmentOrder],
        ConfigSlot = WorkflowSlotCodes.S6QueueAndCancelPolicy,
    };

    /// <summary>System creates a treatment order draft once the schedule is confirmed.</summary>
    public static readonly TransitionDefinition TRT_003 = new()
    {
        Code = "TRT-003",
        FromStatuses = [CaseStatus.Scheduled],
        ToStatus = CaseStatus.OrderPending,
        TriggerName = "PrepareTreatmentOrder",
        TriggerType = WorkflowTriggerType.System,
        RequiredRole = null,
        GateChecks = ["ScheduleExists"],
        SuccessActions = ["CreateOrderDraft"],
        WorkItemsToCreate = [WorkItemTypes.TreatmentOrder],
    };

    /// <summary>Doctor submits the treatment order to the treatment management system.</summary>
    public static readonly TransitionDefinition TRT_004 = new()
    {
        Code = "TRT-004",
        FromStatuses = [CaseStatus.OrderPending],
        ToStatus = CaseStatus.OrderSubmitted,
        TriggerName = "SubmitTreatmentOrder",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Doctor",
        GateChecks = ["TreatmentOrderFormValid"],
        SuccessActions = ["SaveOrderForm"],
        FailureActions = ["StayInOrderPending"],
        WorkItemsToCreate = [WorkItemTypes.QueueCall],
    };

    /// <summary>
    /// External queue system creates a treatment queue entry for the patient.
    /// Governed by <see cref="WorkflowSlotCodes.S6QueueAndCancelPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition TRT_005 = new()
    {
        Code = "TRT-005",
        FromStatuses = [CaseStatus.OrderSubmitted],
        ToStatus = CaseStatus.QueuePending,
        TriggerName = "QueueCreated",
        TriggerType = WorkflowTriggerType.ExternalEvent,
        RequiredRole = null,
        GateChecks = ["QueueOrAppointmentValid"],
        SuccessActions = ["SaveQueueRef"],
        FailureActions = ["RetryOrLocalFallback"],
        WorkItemsToCreate = [WorkItemTypes.QueueCall],
        ConfigSlot = WorkflowSlotCodes.S6QueueAndCancelPolicy,
    };

    /// <summary>Treatment system notifies that the patient has started treatment.</summary>
    public static readonly TransitionDefinition TRT_006 = new()
    {
        Code = "TRT-006",
        FromStatuses = [CaseStatus.QueuePending],
        ToStatus = CaseStatus.Treating,
        TriggerName = "TreatmentStarted",
        TriggerType = WorkflowTriggerType.ExternalEvent,
        RequiredRole = null,
        GateChecks = ["TreatmentStartEventValid"],
        SuccessActions = ["CreateTreatmentMonitor"],
        FailureActions = ["RejectEvent"],
        WorkItemsToCreate = [WorkItemTypes.TreatmentMonitor],
    };

    /// <summary>
    /// Treatment system reports completion of an individual treatment fraction.
    /// This is an idempotent self-transition; duplicate events are safely ignored.
    /// Governed by <see cref="WorkflowSlotCodes.S7TreatmentCompletionPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition TRT_007 = new()
    {
        Code = "TRT-007",
        FromStatuses = [CaseStatus.Treating],
        ToStatus = CaseStatus.Treating,
        TriggerName = "TreatmentFractionCompleted",
        TriggerType = WorkflowTriggerType.ExternalEvent,
        RequiredRole = null,
        GateChecks = ["FractionDataValid"],
        SuccessActions = ["UpdateProgress"],
        FailureActions = ["IgnoreDuplicate"],
        WorkItemsToCreate = [WorkItemTypes.TreatmentMonitor],
        ConfigSlot = WorkflowSlotCodes.S7TreatmentCompletionPolicy,
    };

    /// <summary>
    /// Therapist or system pauses treatment (e.g. for a setup adjustment).
    /// Governed by <see cref="WorkflowSlotCodes.S8ExceptionHandlingPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition TRT_008 = new()
    {
        Code = "TRT-008",
        FromStatuses = [CaseStatus.Treating],
        ToStatus = CaseStatus.TreatmentPaused,
        TriggerName = "PauseTreatment",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Therapist/System",
        GateChecks = ["PauseReasonProvided"],
        SuccessActions = ["AuditPause"],
        FailureActions = ["RejectTransition"],
        WorkItemsToCreate = [WorkItemTypes.TreatmentExceptionHandling],
        ConfigSlot = WorkflowSlotCodes.S8ExceptionHandlingPolicy,
    };

    /// <summary>
    /// Therapist or system resumes treatment after a pause has been resolved.
    /// Governed by <see cref="WorkflowSlotCodes.S8ExceptionHandlingPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition TRT_009 = new()
    {
        Code = "TRT-009",
        FromStatuses = [CaseStatus.TreatmentPaused],
        ToStatus = CaseStatus.Treating,
        TriggerName = "ResumeTreatment",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Therapist/System",
        GateChecks = ["ResumeAllowed"],
        SuccessActions = ["CloseExceptionTaskIfResolved"],
        FailureActions = ["StayInTreatmentPaused"],
        WorkItemsToCreate = [WorkItemTypes.TreatmentMonitor],
        ConfigSlot = WorkflowSlotCodes.S8ExceptionHandlingPolicy,
    };

    /// <summary>
    /// Therapist, doctor, or system interrupts treatment requiring clinical review before resuming.
    /// Governed by <see cref="WorkflowSlotCodes.S8ExceptionHandlingPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition TRT_010 = new()
    {
        Code = "TRT-010",
        FromStatuses = [CaseStatus.Treating],
        ToStatus = CaseStatus.TreatmentInterrupted,
        TriggerName = "InterruptTreatment",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Therapist/Doctor/System",
        GateChecks = ["InterruptionReasonRequired"],
        SuccessActions = ["AuditInterruption"],
        FailureActions = ["RejectTransition"],
        WorkItemsToCreate = [WorkItemTypes.TreatmentExceptionHandling],
        ConfigSlot = WorkflowSlotCodes.S8ExceptionHandlingPolicy,
    };

    /// <summary>
    /// Doctor or therapist resumes treatment after an interruption with medical approval.
    /// Governed by <see cref="WorkflowSlotCodes.S8ExceptionHandlingPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition TRT_011 = new()
    {
        Code = "TRT-011",
        FromStatuses = [CaseStatus.TreatmentInterrupted],
        ToStatus = CaseStatus.Treating,
        TriggerName = "ResumeAfterInterruption",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Doctor/Therapist",
        GateChecks = ["MedicalApprovalExists"],
        SuccessActions = ["ResumeMonitor"],
        FailureActions = ["StayInInterrupted"],
        WorkItemsToCreate = [WorkItemTypes.TreatmentMonitor],
        ConfigSlot = WorkflowSlotCodes.S8ExceptionHandlingPolicy,
    };

    /// <summary>
    /// Treatment system confirms that all fractions have been delivered per the completion policy.
    /// Governed by <see cref="WorkflowSlotCodes.S7TreatmentCompletionPolicy"/>.
    /// </summary>
    public static readonly TransitionDefinition TRT_012 = new()
    {
        Code = "TRT-012",
        FromStatuses = [CaseStatus.Treating],
        ToStatus = CaseStatus.TreatmentCompleted,
        TriggerName = "CompleteTreatmentCourse",
        TriggerType = WorkflowTriggerType.ExternalEvent,
        RequiredRole = null,
        GateChecks = ["S7CompletionRuleSatisfied"],
        SuccessActions = ["CreatePostReview"],
        FailureActions = ["StayInTreating"],
        WorkItemsToCreate = [WorkItemTypes.PostTreatmentReview],
        ConfigSlot = WorkflowSlotCodes.S7TreatmentCompletionPolicy,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 9 – Post-treatment & Archiving
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>System automatically creates a post-treatment review task after treatment completes.</summary>
    public static readonly TransitionDefinition POST_001 = new()
    {
        Code = "POST-001",
        FromStatuses = [CaseStatus.TreatmentCompleted],
        ToStatus = CaseStatus.PostTreatmentReviewPending,
        TriggerName = "StartPostTreatmentReview",
        TriggerType = WorkflowTriggerType.System,
        RequiredRole = null,
        GateChecks = ["TreatmentCompleted"],
        SuccessActions = ["CreateReviewTask"],
        WorkItemsToCreate = [WorkItemTypes.PostTreatmentReview],
    };

    /// <summary>Doctor submits the post-treatment review form, completing the review phase.</summary>
    public static readonly TransitionDefinition POST_002 = new()
    {
        Code = "POST-002",
        FromStatuses = [CaseStatus.PostTreatmentReviewPending],
        ToStatus = CaseStatus.PostTreatmentReviewed,
        TriggerName = "SubmitPostTreatmentReview",
        TriggerType = WorkflowTriggerType.User,
        RequiredRole = "Doctor",
        GateChecks = ["PostTreatmentReviewFormValid"],
        SuccessActions = ["SaveForm"],
        FailureActions = ["StayInReviewPending"],
        WorkItemsToCreate = [WorkItemTypes.ArchiveReview],
    };

    /// <summary>
    /// System or admin archives the case once all tasks are complete and required forms submitted.
    /// </summary>
    public static readonly TransitionDefinition POST_003 = new()
    {
        Code = "POST-003",
        FromStatuses = [CaseStatus.PostTreatmentReviewed],
        ToStatus = CaseStatus.Archived,
        TriggerName = "ArchiveCase",
        TriggerType = WorkflowTriggerType.System,
        RequiredRole = "System/Admin",
        GateChecks = ["NoBlockingTasks", "RequiredFormsComplete"],
        SuccessActions = ["MarkCaseReadOnly"],
        FailureActions = ["RejectArchive"],
        WorkItemsToCreate = [],
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
        SIM_001, SIM_002, SIM_003, SIM_004, SIM_005,
        // Image Acquisition
        IMG_001, IMG_002, IMG_003,
        // Contouring
        CON_001, CON_002, CON_003, CON_004, CON_005,
        // Contour Review
        REV_001, REV_002, REV_003, REV_004,
        // Treatment Planning
        PLN_001, PLN_002, PLN_003, PLN_004, PLN_005, PLN_006,
        // Re-review & Prescription
        RX_001, RX_002, RX_003, RX_004, RX_005, RX_006, RX_007,
        // Plan QA & Double-check
        QA_001, QA_002, QA_003, QA_004, QA_005, QA_006, QA_007, QA_008,
        // Scheduling, Order & Treatment
        TRT_001, TRT_002, TRT_003, TRT_004, TRT_005, TRT_006,
        TRT_007, TRT_008, TRT_009, TRT_010, TRT_011, TRT_012,
        // Post-treatment & Archiving
        POST_001, POST_002, POST_003,
    ]);

    /// <summary>
    /// All transition definitions keyed by <see cref="TransitionDefinition.Code"/> for O(1) lookup.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, TransitionDefinition> ByCode =
        new ReadOnlyDictionary<string, TransitionDefinition>(
            All.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase));
}
