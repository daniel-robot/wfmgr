using System.Collections.ObjectModel;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Application.Workflows.V1.WorkItems;
using Wfmgr.Domain.Enums;
using Wfmgr.Domain.Forms;
using Wfmgr.Domain.WorkItems;

namespace Wfmgr.Application.Workflows.V1.Gates;

/// <summary>
/// Evaluates all named gate checks declared on a <see cref="TransitionDefinition"/> before
/// a workflow case is allowed to change status.
/// <para>
/// Each gate check is registered in a strategy map keyed by the constants in
/// <see cref="GateCheckNames"/>. A check returns <c>null</c> when it passes, or a
/// human-readable failure message when it does not.  All checks are evaluated and failures
/// are collected rather than short-circuiting on the first failure, giving callers a
/// complete picture of what is blocking a transition.
/// </para>
/// <para>
/// Gate checks that are not present in the strategy map produce a failure with the message
/// "not implemented", preventing silent pass-through of unknown check names.
/// </para>
/// </summary>
public sealed class GateValidationService : IGateValidationService
{
    private readonly IWorkflowDataAccess _dataAccess;
    private readonly IWorkflowProfileResolver _profileResolver;

    // A single delegate type representing any individual gate check.
    private delegate Task<string?> GateCheck(CaseData d, GateValidationContext ctx, CancellationToken ct);

    private readonly IReadOnlyDictionary<string, GateCheck> _checks;

    // Case statuses from which cancellation is permitted (mirrors CaseStateMachineService).
    private static readonly HashSet<CaseStatus> CancellableStatuses =
    [
        CaseStatus.Draft,
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
        CaseStatus.QueuePending,
    ];

    public GateValidationService(
        IWorkflowDataAccess dataAccess,
        IWorkflowProfileResolver profileResolver)
    {
        _dataAccess = dataAccess;
        _profileResolver = profileResolver;
        _checks = BuildCheckMap();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<GateValidationResult> ValidateAsync(
        CaseData caseData,
        TransitionDefinition transition,
        GateValidationContext context,
        CancellationToken ct = default)
    {
        var failedChecks = new List<string>();
        var messages = new List<string>();

        foreach (var name in transition.GateChecks)
        {
            if (!_checks.TryGetValue(name, out var check))
            {
                failedChecks.Add(name);
                messages.Add($"Gate check '{name}' is not implemented in {nameof(GateValidationService)}.");
                continue;
            }

            var failure = await check(caseData, context, ct);
            if (failure is not null)
            {
                failedChecks.Add(name);
                messages.Add(failure);
            }
        }

        return failedChecks.Count == 0
            ? GateValidationResult.Success()
            : GateValidationResult.Failure(failedChecks.AsReadOnly(), messages.AsReadOnly());
    }

    // ── Strategy map builder ──────────────────────────────────────────────────

    private IReadOnlyDictionary<string, GateCheck> BuildCheckMap()
    {
        var m = new Dictionary<string, GateCheck>(StringComparer.OrdinalIgnoreCase);

        // ── Simulation ────────────────────────────────────────────────────────
        m[GateCheckNames.SimulationRequestFormValid] = SimulationRequestFormValidAsync;
        m[GateCheckNames.SimulationRecordFormValid]  = SimulationRecordFormValidAsync;
        m[GateCheckNames.SimulationScheduleExists]   = SimulationScheduleExistsAsync;

        // ── Case state ────────────────────────────────────────────────────────
        m[GateCheckNames.CaseNotCancelled]      = CaseNotCancelledAsync;
        m[GateCheckNames.CaseActiveNotCancelled] = CaseNotCancelledAsync; // alias
        m[GateCheckNames.CancellationAllowed]    = CancellationAllowedAsync;
        m[GateCheckNames.TreatmentNotStarted]    = CancellationAllowedAsync; // alias

        // ── Image acquisition ─────────────────────────────────────────────────
        m[GateCheckNames.ImageReferenceExists]            = ImageReferenceExistsAsync;
        m[GateCheckNames.ImageRefsValid]                  = ImageReferenceExistsAsync; // alias
        m[GateCheckNames.ImageAccessible]                 = ImageReferenceExistsAsync; // alias (reference-level check)
        m[GateCheckNames.CaseResolvedByCorrelationKey]    = ExternalPayloadPresentAsync;
        m[GateCheckNames.ExternalAcceptOrDeliveryConfirmed] = ExternalPayloadPresentAsync;

        // ── Contouring ────────────────────────────────────────────────────────
        m[GateCheckNames.ContourResultExists]      = ContourResultExistsAsync;
        m[GateCheckNames.ContourResultRefsValid]   = ContourResultExistsAsync; // alias
        m[GateCheckNames.RevisedContourExists]     = ContourResultExistsAsync; // alias
        m[GateCheckNames.EventValid]               = ExternalPayloadPresentAsync;
        m[GateCheckNames.EventIdempotent]          = EventIdempotentAsync;
        m[GateCheckNames.ManualContourPayloadValid] = FormOrPayloadPresentAsync;
        m[GateCheckNames.RetryAllowed]             = RetryAllowedAsync;

        // ── Contour review ────────────────────────────────────────────────────
        m[GateCheckNames.ReviewApprovalExists]   = ReviewApprovalExistsAsync;
        m[GateCheckNames.MinimumApprovalsReached] = ReviewApprovalExistsAsync; // alias
        m[GateCheckNames.RejectionReasonRequired] = ReasonPresentAsync;

        // ── Treatment planning ────────────────────────────────────────────────
        m[GateCheckNames.PlanVersionExists]    = PlanVersionExistsAsync;
        m[GateCheckNames.PlanPayloadValid]     = FormOrPayloadPresentAsync;
        m[GateCheckNames.AssigneeExists]       = AssigneeExistsAsync;
        m[GateCheckNames.TaskAssigned]         = WorkItemIdPresentAsync;
        m[GateCheckNames.PlanEvaluationApproved] = PlanEvaluationApprovedAsync;
        m[GateCheckNames.EvaluationApproved]   = PlanEvaluationApprovedAsync; // alias
        m[GateCheckNames.ReasonRequired]       = ReasonPresentAsync;
        m[GateCheckNames.FailureReasonRequired] = ReasonPresentAsync; // alias
        m[GateCheckNames.ReworkDecisionMade]   = ReasonPresentAsync; // alias

        // ── Re-review & prescription ──────────────────────────────────────────
        m[GateCheckNames.S4ReReviewEnabled]          = S4ReReviewEnabledAsync;
        m[GateCheckNames.NoReReviewRequired]         = S4ReReviewDisabledAsync;
        m[GateCheckNames.ReReviewApproved]           = ReReviewApprovedAsync;
        m[GateCheckNames.PrescriptionReferenceExists] = PrescriptionReferenceExistsAsync;
        m[GateCheckNames.PrescriptionReferenceValid]  = PrescriptionReferenceExistsAsync; // alias
        m[GateCheckNames.FailureEventValid]           = ExternalPayloadPresentAsync;

        // ── Plan QA ───────────────────────────────────────────────────────────
        m[GateCheckNames.PlanAndPrescriptionPresent] = PlanAndPrescriptionPresentAsync;
        m[GateCheckNames.QAFormApproved]            = QAApprovalExistsAsync;
        m[GateCheckNames.QAFormValid]               = QAApprovalExistsAsync; // alias

        // ── Double-check ──────────────────────────────────────────────────────
        m[GateCheckNames.DoubleCheckApproved]  = DoubleCheckApprovedAsync;
        m[GateCheckNames.S5DoubleCheckEnabled] = S5DoubleCheckEnabledAsync;
        m[GateCheckNames.S5DoubleCheckDisabled] = S5DoubleCheckDisabledAsync;

        // ── Scheduling ────────────────────────────────────────────────────────
        m[GateCheckNames.ScheduleReferenceExists] = ScheduleReferenceExistsAsync;
        m[GateCheckNames.ScheduleExists]          = ScheduleReferenceExistsAsync; // alias
        m[GateCheckNames.CaseReleasedForSchedule] = ScheduleReferenceExistsAsync; // alias
        m[GateCheckNames.SchedulePayloadValid]    = ExternalPayloadPresentAsync;

        // ── Treatment order & execution ───────────────────────────────────────
        m[GateCheckNames.TreatmentOrderFormValid]   = TreatmentOrderFormValidAsync;
        m[GateCheckNames.QueueOrAppointmentValid]   = ExternalPayloadPresentAsync;
        m[GateCheckNames.TreatmentStartEventValid]  = ExternalPayloadPresentAsync;
        m[GateCheckNames.FractionDataValid]         = ExternalPayloadPresentAsync;
        m[GateCheckNames.PauseReasonProvided]       = ReasonPresentAsync;
        m[GateCheckNames.ResumeAllowed]             = ResumeAllowedAsync;
        m[GateCheckNames.InterruptionReasonRequired] = ReasonPresentAsync;
        m[GateCheckNames.MedicalApprovalExists]     = MedicalApprovalExistsAsync;
        m[GateCheckNames.TreatmentCompletionSatisfied] = TreatmentCompletionSatisfiedAsync;
        m[GateCheckNames.S7CompletionRuleSatisfied] = TreatmentCompletionSatisfiedAsync; // alias
        m[GateCheckNames.TreatmentCompleted]        = TreatmentCompletedStatusAsync;

        // ── Post-treatment & archiving ────────────────────────────────────────
        m[GateCheckNames.PostTreatmentReviewFormValid] = PostTreatmentReviewFormValidAsync;
        m[GateCheckNames.NoBlockingTasks]              = NoBlockingTasksAsync;
        m[GateCheckNames.RequiredFormsComplete]        = PostTreatmentReviewFormValidAsync; // overlap

        return new ReadOnlyDictionary<string, GateCheck>(m);
    }

    // ── Gate check implementations ────────────────────────────────────────────
    //
    // Convention: return null  → check passed.
    //             return string → check failed; value is the human-readable reason.

    // ── Simulation ────────────────────────────────────────────────────────────

    private async Task<string?> SimulationRequestFormValidAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var ok = await _dataAccess.CaseFormExistsAsync(
            d.CaseId, CaseFormTypes.SimulationRequestForm, CaseFormStatuses.Submitted, ct);
        return ok ? null : "A submitted SimulationRequestForm does not exist for this case.";
    }

    private async Task<string?> SimulationRecordFormValidAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var ok = await _dataAccess.CaseFormExistsAsync(
            d.CaseId, CaseFormTypes.SimulationRecordForm, CaseFormStatuses.Submitted, ct);
        return ok ? null : "A submitted SimulationRecordForm does not exist for this case.";
    }

    private async Task<string?> SimulationScheduleExistsAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        // Any non-cancelled simulation schedule work item is sufficient.
        var ok = await _dataAccess.WorkItemExistsAsync(d.CaseId, WorkItemTypes.SimulationSchedule, null, ct);
        return ok ? null : "A SimulationSchedule work item does not exist for this case.";
    }

    // ── Case state ────────────────────────────────────────────────────────────

    private static Task<string?> CaseNotCancelledAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var isCancelled = d.CurrentStatus is CaseStatus.Cancelled or CaseStatus.Archived;
        return Task.FromResult<string?>(
            isCancelled ? $"Case is in terminal status '{d.CurrentStatus}' and cannot be transitioned." : null);
    }

    private static Task<string?> CancellationAllowedAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var allowed = CancellableStatuses.Contains(d.CurrentStatus);
        return Task.FromResult<string?>(
            allowed ? null : $"Cancellation is not permitted once the case has reached status '{d.CurrentStatus}'.");
    }

    // ── Image acquisition ─────────────────────────────────────────────────────

    private static Task<string?> ImageReferenceExistsAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var missing = string.IsNullOrWhiteSpace(d.CtStudyInstanceUid)
                   || string.IsNullOrWhiteSpace(d.CtWadoRsUrl);
        return Task.FromResult<string?>(
            missing ? "CT image references (StudyInstanceUID and WADO-RS URL) are not present on the case." : null);
    }

    // ── Contouring ────────────────────────────────────────────────────────────

    private static Task<string?> ContourResultExistsAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var missing = string.IsNullOrWhiteSpace(d.RtStructSeriesInstanceUid);
        return Task.FromResult<string?>(
            missing ? "RTStruct contour result references are not present on the case." : null);
    }

    private async Task<string?> EventIdempotentAsync(CaseData d, GateValidationContext ctx, CancellationToken ct)
    {
        if (!ctx.Metadata.TryGetValue(GateCheckNames.MetaEventSource, out var src) || src is not string source
         || !ctx.Metadata.TryGetValue(GateCheckNames.MetaEventType,   out var typ) || typ is not string type
         || !ctx.Metadata.TryGetValue(GateCheckNames.MetaEventExternalId, out var eid) || eid is not string externalId)
        {
            // Required metadata absent — cannot perform dedup check; allow through.
            return null;
        }

        var isDuplicate = await _dataAccess.ExternalEventExistsAsync(source, type, externalId, ct);
        return isDuplicate
            ? $"External event '{type}' from '{source}' with id '{externalId}' has already been processed."
            : null;
    }

    private static Task<string?> RetryAllowedAsync(CaseData _, GateValidationContext ctx, CancellationToken ct)
    {
        if (ctx.Metadata.TryGetValue(GateCheckNames.MetaRetryAllowed, out var val)
            && val is bool allowed && !allowed)
        {
            return Task.FromResult<string?>("Retry is not permitted for this operation per the current policy.");
        }

        // If the metadata key is absent, allow by default.
        return Task.FromResult<string?>(null);
    }

    // ── Generic context checks ────────────────────────────────────────────────

    private static Task<string?> ExternalPayloadPresentAsync(CaseData _, GateValidationContext ctx, CancellationToken ct)
    {
        var missing = string.IsNullOrWhiteSpace(ctx.ExternalEventPayload);
        return Task.FromResult<string?>(missing ? "An external event payload is required but was not provided." : null);
    }

    private static Task<string?> FormOrPayloadPresentAsync(CaseData _, GateValidationContext ctx, CancellationToken ct)
    {
        var hasForm    = ctx.FormId.HasValue;
        var hasPayload = !string.IsNullOrWhiteSpace(ctx.ExternalEventPayload);
        return Task.FromResult<string?>(
            (hasForm || hasPayload) ? null : "A form ID or external event payload is required but was not provided.");
    }

    private static Task<string?> ReasonPresentAsync(CaseData _, GateValidationContext ctx, CancellationToken ct)
    {
        var missing = string.IsNullOrWhiteSpace(ctx.Reason);
        return Task.FromResult<string?>(missing ? "A reason or rejection note is required but was not provided." : null);
    }

    private static Task<string?> AssigneeExistsAsync(CaseData _, GateValidationContext ctx, CancellationToken ct)
    {
        var hasInlineUser = !string.IsNullOrWhiteSpace(ctx.UserId);
        var hasMetaUser   = ctx.Metadata.TryGetValue(GateCheckNames.MetaAssigneeUserId, out var u)
                         && u is string s && !string.IsNullOrWhiteSpace(s);

        return Task.FromResult<string?>(
            (hasInlineUser || hasMetaUser) ? null : "An assignee user ID must be supplied in UserId or metadata.");
    }

    private static Task<string?> WorkItemIdPresentAsync(CaseData _, GateValidationContext ctx, CancellationToken ct)
    {
        return Task.FromResult<string?>(
            ctx.WorkItemId.HasValue ? null : "A work item ID must be supplied in the execution context.");
    }

    // ── Contour review ────────────────────────────────────────────────────────

    private async Task<string?> ReviewApprovalExistsAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var hasWorkItem = await _dataAccess.WorkItemExistsAsync(
            d.CaseId, WorkItemTypes.ContourReview, WorkItemResultCodes.Approved, ct);
        if (hasWorkItem) return null;

        var hasForm = await _dataAccess.CaseFormExistsAsync(
            d.CaseId, CaseFormTypes.ContourReviewForm, CaseFormStatuses.Submitted, ct);
        return hasForm ? null : "A contour review approval (work item or form) does not exist for this case.";
    }

    // ── Treatment planning ────────────────────────────────────────────────────

    private async Task<string?> PlanVersionExistsAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        if (d.CurrentPlanVersionNo.HasValue) return null;

        var ok = await _dataAccess.PlanVersionExistsAsync(d.CaseId, ct);
        return ok ? null : "A treatment plan version does not exist for this case.";
    }

    private async Task<string?> PlanEvaluationApprovedAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var hasWorkItem = await _dataAccess.WorkItemExistsAsync(
            d.CaseId, WorkItemTypes.PlanEvaluation, WorkItemResultCodes.Approved, ct);
        if (hasWorkItem) return null;

        var hasForm = await _dataAccess.CaseFormExistsAsync(
            d.CaseId, CaseFormTypes.PlanEvaluationForm, CaseFormStatuses.Submitted, ct);
        return hasForm ? null : "A plan evaluation approval (work item or form) does not exist for this case.";
    }

    // ── Re-review & prescription ──────────────────────────────────────────────

    private async Task<string?> S4ReReviewEnabledAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var policy = await _profileResolver.ResolveS4PlanReReviewPolicyAsync(
            d.HospitalId, d.SiteId, d.DepartmentId, ct);
        return policy.Enabled
            ? null
            : "S4 plan re-review policy is not enabled for this organisation; transition requires it to be enabled.";
    }

    private async Task<string?> S4ReReviewDisabledAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var policy = await _profileResolver.ResolveS4PlanReReviewPolicyAsync(
            d.HospitalId, d.SiteId, d.DepartmentId, ct);
        return !policy.Enabled
            ? null
            : "S4 plan re-review policy is enabled; prescription generation must await re-review approval.";
    }

    private async Task<string?> ReReviewApprovedAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var hasWorkItem = await _dataAccess.WorkItemExistsAsync(
            d.CaseId, WorkItemTypes.PlanReReview, WorkItemResultCodes.Approved, ct);
        if (hasWorkItem) return null;

        var hasForm = await _dataAccess.CaseFormExistsAsync(
            d.CaseId, CaseFormTypes.PlanReReviewForm, CaseFormStatuses.Submitted, ct);
        return hasForm ? null : "A plan re-review approval (work item or form) does not exist for this case.";
    }

    private async Task<string?> PrescriptionReferenceExistsAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var ok = await _dataAccess.WorkItemExistsAsync(
            d.CaseId, WorkItemTypes.PrescriptionSync, WorkItemResultCodes.Synced, ct);
        return ok ? null : "A successfully synchronised prescription reference does not exist for this case.";
    }

    // ── Plan QA ───────────────────────────────────────────────────────────────

    private async Task<string?> PlanAndPrescriptionPresentAsync(CaseData d, GateValidationContext ctx, CancellationToken ct)
    {
        var planFailure = await PlanVersionExistsAsync(d, ctx, ct);
        if (planFailure is not null) return planFailure;

        var rxFailure = await PrescriptionReferenceExistsAsync(d, ctx, ct);
        return rxFailure is not null
            ? "A prescription reference must exist before QA can begin. " + rxFailure
            : null;
    }

    private async Task<string?> QAApprovalExistsAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var hasWorkItem = await _dataAccess.WorkItemExistsAsync(
            d.CaseId, WorkItemTypes.PlanQA, WorkItemResultCodes.Approved, ct);
        if (hasWorkItem) return null;

        var hasForm = await _dataAccess.CaseFormExistsAsync(
            d.CaseId, CaseFormTypes.PlanQAForm, CaseFormStatuses.Submitted, ct);
        return hasForm ? null : "A plan QA approval (work item or form) does not exist for this case.";
    }

    // ── Double-check ──────────────────────────────────────────────────────────

    private async Task<string?> DoubleCheckApprovedAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var hasWorkItem = await _dataAccess.WorkItemExistsAsync(
            d.CaseId, WorkItemTypes.PlanDoubleCheck, WorkItemResultCodes.Approved, ct);
        if (hasWorkItem) return null;

        var hasForm = await _dataAccess.CaseFormExistsAsync(
            d.CaseId, CaseFormTypes.PlanDoubleCheckForm, CaseFormStatuses.Submitted, ct);
        return hasForm ? null : "A plan double-check approval (work item or form) does not exist for this case.";
    }

    private async Task<string?> S5DoubleCheckEnabledAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var policy = await _profileResolver.ResolveS5PlanDoubleCheckPolicyAsync(
            d.HospitalId, d.SiteId, d.DepartmentId, ct);
        return policy.Enabled
            ? null
            : "S5 double-check policy is not enabled for this organisation; transition requires it to be enabled.";
    }

    private async Task<string?> S5DoubleCheckDisabledAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var policy = await _profileResolver.ResolveS5PlanDoubleCheckPolicyAsync(
            d.HospitalId, d.SiteId, d.DepartmentId, ct);
        return !policy.Enabled
            ? null
            : "S5 double-check policy is enabled; case must complete the double check before scheduling.";
    }

    // ── Scheduling ────────────────────────────────────────────────────────────

    private async Task<string?> ScheduleReferenceExistsAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var ok = await _dataAccess.WorkItemExistsAsync(
            d.CaseId, WorkItemTypes.ScheduleSync, WorkItemResultCodes.Synced, ct);
        return ok ? null : "A synchronised schedule reference does not exist for this case.";
    }

    // ── Treatment order & execution ───────────────────────────────────────────

    private async Task<string?> TreatmentOrderFormValidAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var hasForm = await _dataAccess.CaseFormExistsAsync(
            d.CaseId, CaseFormTypes.TreatmentOrderForm, CaseFormStatuses.Submitted, ct);
        if (hasForm) return null;

        var hasWorkItem = await _dataAccess.WorkItemExistsAsync(
            d.CaseId, WorkItemTypes.TreatmentOrder, WorkItemResultCodes.Submitted, ct);
        return hasWorkItem ? null : "A submitted treatment order (form or work item) does not exist for this case.";
    }

    private static Task<string?> ResumeAllowedAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        // The case must be in the paused state for a resume to be valid.
        var ok = d.CurrentStatus is CaseStatus.TreatmentPaused or CaseStatus.TreatmentInterrupted;
        return Task.FromResult<string?>(
            ok ? null : $"Case is not in a paused or interrupted state (current status: '{d.CurrentStatus}').");
    }

    private async Task<string?> MedicalApprovalExistsAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        // Medical approval after an interruption is represented by a completed TreatmentOrder work item
        // or, in future, a dedicated approval form.  Using the order as the closest proxy.
        var ok = await _dataAccess.WorkItemExistsAsync(
            d.CaseId, WorkItemTypes.TreatmentOrder, WorkItemResultCodes.Approved, ct);
        return ok ? null : "Medical approval for resuming after interruption has not been recorded.";
    }

    private async Task<string?> TreatmentCompletionSatisfiedAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var ok = await _dataAccess.WorkItemExistsAsync(
            d.CaseId, WorkItemTypes.TreatmentMonitor, WorkItemResultCodes.Reviewed, ct);
        return ok ? null : "Treatment completion criteria have not been satisfied (no completed TreatmentMonitor work item).";
    }

    private static Task<string?> TreatmentCompletedStatusAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var ok = d.CurrentStatus == CaseStatus.TreatmentCompleted;
        return Task.FromResult<string?>(
            ok ? null : $"Case must be in TreatmentCompleted status (current: '{d.CurrentStatus}').");
    }

    // ── Post-treatment & archiving ────────────────────────────────────────────

    private async Task<string?> PostTreatmentReviewFormValidAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var hasForm = await _dataAccess.CaseFormExistsAsync(
            d.CaseId, CaseFormTypes.PostTreatmentReviewForm, CaseFormStatuses.Submitted, ct);
        if (hasForm) return null;

        var hasWorkItem = await _dataAccess.WorkItemExistsAsync(
            d.CaseId, WorkItemTypes.PostTreatmentReview, WorkItemResultCodes.Reviewed, ct);
        return hasWorkItem ? null : "A submitted post-treatment review (form or work item) does not exist for this case.";
    }

    private async Task<string?> NoBlockingTasksAsync(CaseData d, GateValidationContext _, CancellationToken ct)
    {
        var openItems = await _dataAccess.GetMutableWorkItemsByCaseIdAsync(d.CaseId, ct);
        var blocking  = openItems
            .Where(w => w.Status is WorkItemStatus.Pending or WorkItemStatus.InProgress)
            .Select(w => w.Type)
            .ToList();

        return blocking.Count == 0
            ? null
            : $"The following work items are still open and must be resolved before archiving: {string.Join(", ", blocking)}.";
    }
}
