using System.Collections.ObjectModel;
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Domain.Enums;
using Wfmgr.Domain.WorkItems;

namespace Wfmgr.Application.Workflows.V1;

/// <summary>
/// Central catalog of every named compensation rule in the radiotherapy case lifecycle.
/// <para>
/// Each <see cref="CompensationDefinition"/> describes what the system should do when a
/// particular workflow step fails, including the recovery action, target status to restore,
/// optional work item to create, and retry guidance.
/// </para>
/// <para>
/// Use <see cref="ByCode"/> for O(1) lookup by code, or iterate <see cref="All"/> for
/// documentation, validation, or alerting bootstrap.
/// </para>
/// <para>
/// This catalog is intentionally a static definitions store.  Compensation execution logic
/// is not yet implemented; definitions are provided here as a foundation for that work.
/// </para>
/// </summary>
public static class WorkflowCompensationCatalog
{
    // ─────────────────────────────────────────────────────────────────────────
    // Image & Forwarding Compensations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// IMG-002 outbox send to contouring tool failed.
    /// Retries with exponential back-off; falls back to a manual-forward work item.
    /// </summary>
    public static readonly CompensationDefinition CMP_001 = new()
    {
        Code = "CMP-001",
        FailedStepCode = "IMG-002",
        FailureCondition = "Outbox send to contouring tool failed",
        CompensationAction = "Retry send with exponential back-off; if retry limit exceeded, fall back to manual forward",
        TargetStatus = CaseStatus.ImageForwarding,
        WorkItemToCreate = WorkItemTypes.ImageForwardToContourTool,
        ManualInterventionRequired = true,
        RetryPolicy = RetryPolicy.ExponentialBackoff,
    };

    /// <summary>
    /// IMG-003 contouring tool not accepting the forwarded images.
    /// Keeps the case safe at ImageStored status and allows manual resend.
    /// </summary>
    public static readonly CompensationDefinition CMP_002 = new()
    {
        Code = "CMP-002",
        FailedStepCode = "IMG-003",
        FailureCondition = "External contouring tool not accepting images",
        CompensationAction = "Keep case at ImageStored; allow operator to manually resend",
        TargetStatus = CaseStatus.ImageStored,
        WorkItemToCreate = WorkItemTypes.ImageForwardToContourTool,
        ManualInterventionRequired = true,
        RetryPolicy = RetryPolicy.LimitedRetry,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Contouring Compensations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// CON-002 auto-contouring produced an invalid or corrupt result.
    /// Routes the case to ContourReworkRequired and creates a manual-contouring work item.
    /// </summary>
    public static readonly CompensationDefinition CMP_003 = new()
    {
        Code = "CMP-003",
        FailedStepCode = "CON-002",
        FailureCondition = "Auto-contour result is invalid or corrupt",
        CompensationAction = "Request manual contouring; route case to rework-required",
        TargetStatus = CaseStatus.ContourReworkRequired,
        WorkItemToCreate = WorkItemTypes.ManualContouring,
        ManualInterventionRequired = true,
        RetryPolicy = null,
    };

    /// <summary>
    /// CON-003 auto-contouring job on PvMed or third-party system failed entirely.
    /// Creates a manual-contouring work item; retry is optional via configuration.
    /// </summary>
    public static readonly CompensationDefinition CMP_004 = new()
    {
        Code = "CMP-004",
        FailedStepCode = "CON-003",
        FailureCondition = "PvMed or third-party auto-contouring system failed",
        CompensationAction = "Create manual contouring rework work item",
        TargetStatus = CaseStatus.ContourReworkRequired,
        WorkItemToCreate = WorkItemTypes.ManualContouring,
        ManualInterventionRequired = true,
        RetryPolicy = RetryPolicy.LimitedRetry,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Contour Review Compensation
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// REV-003 contour review rejected by clinician.
    /// Routes the case back to ContoursRejected and creates a rework work item.
    /// </summary>
    public static readonly CompensationDefinition CMP_005 = new()
    {
        Code = "CMP-005",
        FailedStepCode = "REV-003",
        FailureCondition = "Contour review rejected by clinician",
        CompensationAction = "Route back for contour rework",
        TargetStatus = CaseStatus.ContoursRejected,
        WorkItemToCreate = WorkItemTypes.ContourRework,
        ManualInterventionRequired = true,
        RetryPolicy = null,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Planning Compensations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// PLN-005 plan evaluation failed; plan sent back for redesign.
    /// Reopens planning path and creates a new plan-design work item.
    /// </summary>
    public static readonly CompensationDefinition CMP_006 = new()
    {
        Code = "CMP-006",
        FailedStepCode = "PLN-005",
        FailureCondition = "Plan evaluation failed during review",
        CompensationAction = "Reopen planning path and create a new plan-design task",
        TargetStatus = CaseStatus.PlanningInProgress,
        WorkItemToCreate = WorkItemTypes.PlanDesign,
        ManualInterventionRequired = true,
        RetryPolicy = null,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Re-review & Prescription Compensations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// RX-004 plan re-review rejected by senior clinician.
    /// Returns the plan to PlanningInProgress for a new design iteration.
    /// </summary>
    public static readonly CompensationDefinition CMP_007 = new()
    {
        Code = "CMP-007",
        FailedStepCode = "RX-004",
        FailureCondition = "Plan re-review failed during secondary clinical review",
        CompensationAction = "Reopen planning with a new plan version; create plan-design task",
        TargetStatus = CaseStatus.PlanningInProgress,
        WorkItemToCreate = WorkItemTypes.PlanDesign,
        ManualInterventionRequired = true,
        RetryPolicy = null,
    };

    /// <summary>
    /// RX-006 prescription synchronisation to external oncology system failed.
    /// Creates a manual sync work item; automated retry is supported.
    /// </summary>
    public static readonly CompensationDefinition CMP_008 = new()
    {
        Code = "CMP-008",
        FailedStepCode = "RX-006",
        FailureCondition = "Prescription synchronisation to oncology system failed",
        CompensationAction = "Create manual prescription-sync task; retry automatically if within policy",
        TargetStatus = CaseStatus.PrescriptionSyncFailed,
        WorkItemToCreate = WorkItemTypes.PrescriptionSync,
        ManualInterventionRequired = true,
        RetryPolicy = RetryPolicy.ExponentialBackoff,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // QA Compensations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// QA-003 QA review failed; returns the case to planning for rework.
    /// </summary>
    public static readonly CompensationDefinition CMP_009 = new()
    {
        Code = "CMP-009",
        FailedStepCode = "QA-003",
        FailureCondition = "Physics QA check failed",
        CompensationAction = "Return case to planning for rework; create plan-design task",
        TargetStatus = CaseStatus.PlanQAFailed,
        WorkItemToCreate = WorkItemTypes.PlanDesign,
        ManualInterventionRequired = true,
        RetryPolicy = null,
    };

    /// <summary>
    /// QA-008 independent double-check rejected; returns the case to planning.
    /// </summary>
    public static readonly CompensationDefinition CMP_010 = new()
    {
        Code = "CMP-010",
        FailedStepCode = "QA-008",
        FailureCondition = "Independent plan double-check failed",
        CompensationAction = "Reopen planning path; create plan-design task",
        TargetStatus = CaseStatus.PlanningInProgress,
        WorkItemToCreate = WorkItemTypes.PlanDesign,
        ManualInterventionRequired = true,
        RetryPolicy = null,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Scheduling & Order Compensations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// TRT-001 MSQ schedule synchronisation timed out or failed.
    /// Retries automatically; falls back to a manual schedule-sync work item.
    /// </summary>
    public static readonly CompensationDefinition CMP_011 = new()
    {
        Code = "CMP-011",
        FailedStepCode = "TRT-001",
        FailureCondition = "MSQ schedule synchronisation timed out or failed",
        CompensationAction = "Retry schedule sync; if retry limit exceeded, create manual scheduling work item",
        TargetStatus = CaseStatus.SchedulingInProgress,
        WorkItemToCreate = WorkItemTypes.ScheduleSync,
        ManualInterventionRequired = true,
        RetryPolicy = RetryPolicy.ExponentialBackoff,
    };

    /// <summary>
    /// TRT-004 treatment order failed form validation on submission.
    /// Case remains at OrderPending until the order is corrected; no auto-retry.
    /// </summary>
    public static readonly CompensationDefinition CMP_012 = new()
    {
        Code = "CMP-012",
        FailedStepCode = "TRT-004",
        FailureCondition = "Treatment order form failed validation on submit",
        CompensationAction = "Remain at OrderPending; require operator to correct and resubmit",
        TargetStatus = CaseStatus.OrderPending,
        WorkItemToCreate = WorkItemTypes.TreatmentOrder,
        ManualInterventionRequired = true,
        RetryPolicy = null,
    };

    /// <summary>
    /// TRT-005 queue integration with external system failed.
    /// Order stays submitted and a local fallback queue work item is created.
    /// </summary>
    public static readonly CompensationDefinition CMP_013 = new()
    {
        Code = "CMP-013",
        FailedStepCode = "TRT-005",
        FailureCondition = "Queue integration with external treatment system failed",
        CompensationAction = "Keep case at OrderSubmitted; allow local or manual queue fallback step",
        TargetStatus = CaseStatus.OrderSubmitted,
        WorkItemToCreate = WorkItemTypes.QueueCall,
        ManualInterventionRequired = true,
        RetryPolicy = RetryPolicy.LimitedRetry,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Treatment Compensations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// TRT-008 paused treatment not resumed within the SLA window.
    /// Timer-based escalation creates an exception-handling work item.
    /// </summary>
    public static readonly CompensationDefinition CMP_014 = new()
    {
        Code = "CMP-014",
        FailedStepCode = "TRT-008",
        FailureCondition = "Paused case not resumed within SLA period",
        CompensationAction = "Create escalation handling work item; notify responsible clinician",
        TargetStatus = CaseStatus.TreatmentPaused,
        WorkItemToCreate = WorkItemTypes.TreatmentExceptionHandling,
        ManualInterventionRequired = true,
        RetryPolicy = RetryPolicy.TimerEscalation,
    };

    /// <summary>
    /// TRT-010 treatment interrupted; requires clinician decision before resuming.
    /// No automated retry — full manual clinical resolution is required.
    /// </summary>
    public static readonly CompensationDefinition CMP_015 = new()
    {
        Code = "CMP-015",
        FailedStepCode = "TRT-010",
        FailureCondition = "Treatment course interrupted",
        CompensationAction = "Open exception-handling work item; require clinician decision before any resume",
        TargetStatus = CaseStatus.TreatmentInterrupted,
        WorkItemToCreate = WorkItemTypes.TreatmentExceptionHandling,
        ManualInterventionRequired = true,
        RetryPolicy = null,
    };

    /// <summary>
    /// TRT-012 treatment-completion data is incomplete; course not yet finishable.
    /// Case remains in Treating and monitoring continues.
    /// </summary>
    public static readonly CompensationDefinition CMP_016 = new()
    {
        Code = "CMP-016",
        FailedStepCode = "TRT-012",
        FailureCondition = "Treatment completion data incomplete per completion policy",
        CompensationAction = "Keep case in Treating; continue monitoring and retry completion check",
        TargetStatus = CaseStatus.Treating,
        WorkItemToCreate = WorkItemTypes.TreatmentMonitor,
        ManualInterventionRequired = false,
        RetryPolicy = new RetryPolicy("PollingRetry", MaxAttempts: 0),
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Post-treatment & Archiving Compensations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// POST-002 post-treatment review form is incomplete.
    /// Case stays at PostTreatmentReviewPending until the form is completed.
    /// </summary>
    public static readonly CompensationDefinition CMP_017 = new()
    {
        Code = "CMP-017",
        FailedStepCode = "POST-002",
        FailureCondition = "Post-treatment review form is incomplete",
        CompensationAction = "Keep case at PostTreatmentReviewPending; require operator to complete form",
        TargetStatus = CaseStatus.PostTreatmentReviewPending,
        WorkItemToCreate = WorkItemTypes.PostTreatmentReview,
        ManualInterventionRequired = true,
        RetryPolicy = null,
    };

    /// <summary>
    /// POST-003 archive attempt blocked by an unresolved task.
    /// Case stays at PostTreatmentReviewed until all blocking tasks are resolved.
    /// </summary>
    public static readonly CompensationDefinition CMP_018 = new()
    {
        Code = "CMP-018",
        FailedStepCode = "POST-003",
        FailureCondition = "One or more blocking tasks remain open at archive time",
        CompensationAction = "Reject archive; keep case at PostTreatmentReviewed until all blocking tasks are resolved",
        TargetStatus = CaseStatus.PostTreatmentReviewed,
        WorkItemToCreate = WorkItemTypes.ArchiveReview,
        ManualInterventionRequired = true,
        RetryPolicy = null,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // General / Cross-phase Compensations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SIM-005 cancel request is medically not allowed at this time.
    /// Transition is rejected; case status is unchanged.
    /// </summary>
    public static readonly CompensationDefinition CMP_019 = new()
    {
        Code = "CMP-019",
        FailedStepCode = "SIM-005",
        FailureCondition = "Cancellation not medically permitted (e.g. treatment already started)",
        CompensationAction = "Reject cancellation; case status remains unchanged",
        TargetStatus = null,
        WorkItemToCreate = null,
        ManualInterventionRequired = true,
        RetryPolicy = null,
    };

    /// <summary>
    /// Any external-event-driven step received a duplicate event.
    /// Event is safely ignored; case status and work items are unchanged.
    /// </summary>
    public static readonly CompensationDefinition CMP_020 = new()
    {
        Code = "CMP-020",
        FailedStepCode = "ANY_EXTERNAL_EVENT",
        FailureCondition = "Duplicate external event received (idempotency check failed)",
        CompensationAction = "Ignore the duplicate event safely; do not change case status or create work items",
        TargetStatus = null,
        WorkItemToCreate = null,
        ManualInterventionRequired = false,
        RetryPolicy = null,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Catalog collections
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// All compensation definitions in canonical order (CMP-001 through CMP-020).
    /// </summary>
    public static readonly IReadOnlyList<CompensationDefinition> All = new ReadOnlyCollection<CompensationDefinition>(
    [
        CMP_001, CMP_002,                                               // Image & Forwarding
        CMP_003, CMP_004,                                               // Contouring
        CMP_005,                                                        // Contour Review
        CMP_006,                                                        // Planning
        CMP_007, CMP_008,                                               // Re-review & Prescription
        CMP_009, CMP_010,                                               // QA
        CMP_011, CMP_012, CMP_013,                                      // Scheduling & Order
        CMP_014, CMP_015, CMP_016,                                      // Treatment
        CMP_017, CMP_018,                                               // Post-treatment & Archiving
        CMP_019, CMP_020,                                               // General
    ]);

    /// <summary>
    /// All compensation definitions keyed by <see cref="CompensationDefinition.Code"/> for O(1) lookup.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, CompensationDefinition> ByCode =
        new ReadOnlyDictionary<string, CompensationDefinition>(
            All.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// All compensation definitions that cover a specific failed-step transition code,
    /// keyed by <see cref="CompensationDefinition.FailedStepCode"/>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<CompensationDefinition>> ByFailedStep =
        new ReadOnlyDictionary<string, IReadOnlyList<CompensationDefinition>>(
            All.GroupBy(c => c.FailedStepCode, StringComparer.OrdinalIgnoreCase)
               .ToDictionary(
                   g => g.Key,
                   g => (IReadOnlyList<CompensationDefinition>)g.ToList().AsReadOnly(),
                   StringComparer.OrdinalIgnoreCase));
}
