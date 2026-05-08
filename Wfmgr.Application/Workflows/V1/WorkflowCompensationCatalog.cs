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
        CMP_002,                                               // Image & Forwarding
        CMP_003, CMP_004,                                               // Contouring
        CMP_005,                                                        // Contour Review
        CMP_006,                                                        // Planning
        CMP_007,                                               // Re-review & Prescription
        CMP_009, CMP_010,                                               // QA
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
