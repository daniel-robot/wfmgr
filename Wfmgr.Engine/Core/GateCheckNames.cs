namespace Wfmgr.Engine.Core;

/// <summary>
/// Human-readable string constants for standard gate-check names.
/// Hosts use the same strings when declaring gate checks in their transition definitions.
/// </summary>
public static class GateCheckNames
{
    // ── Simulation ──────────────────────────────────────────────────────
    public const string SimulationRequestFormValid = "SimulationRequestFormValid";
    public const string SimulationRecordFormValid = "SimulationRecordFormValid";
    public const string SimulationScheduleExists = "SimulationScheduleExists";

    // ── Case state ──────────────────────────────────────────────────────
    public const string CaseNotCancelled = "CaseNotCancelled";
    public const string CaseActiveNotCancelled = "CaseActiveNotCancelled";
    public const string CancellationAllowed = "CancellationAllowed";
    public const string TreatmentNotStarted = "TreatmentNotStarted";

    // ── Image acquisition ───────────────────────────────────────────────
    public const string ImageReferenceExists = "ImageReferenceExists";
    public const string ImageRefsValid = "ImageRefsValid";
    public const string ImageAccessible = "ImageAccessible";
    public const string CaseResolvedByCorrelationKey = "CaseResolvedByCorrelationKey";
    public const string ExternalAcceptOrDeliveryConfirmed = "ExternalAcceptOrDeliveryConfirmed";

    // ── Contouring ──────────────────────────────────────────────────────
    public const string ContourResultExists = "ContourResultExists";
    public const string ContourResultRefsValid = "ContourResultRefsValid";
    public const string RevisedContourExists = "RevisedContourExists";
    public const string EventValid = "EventValid";
    public const string EventIdempotent = "EventIdempotent";
    public const string ManualContourPayloadValid = "ManualContourPayloadValid";
    public const string RetryAllowed = "RetryAllowed";

    // ── Contour review ──────────────────────────────────────────────────
    public const string ReviewApprovalExists = "ReviewApprovalExists";
    public const string MinimumApprovalsReached = "MinimumApprovalsReached";
    public const string RejectionReasonRequired = "RejectionReasonRequired";

    // ── Treatment planning ──────────────────────────────────────────────
    public const string PlanVersionExists = "PlanVersionExists";
    public const string PlanPayloadValid = "PlanPayloadValid";
    public const string AssigneeExists = "AssigneeExists";
    public const string TaskAssigned = "TaskAssigned";
    public const string PlanEvaluationApproved = "PlanEvaluationApproved";
    public const string EvaluationApproved = "EvaluationApproved";
    public const string ReasonRequired = "ReasonRequired";
    public const string FailureReasonRequired = "FailureReasonRequired";
    public const string ReworkDecisionMade = "ReworkDecisionMade";

    // ── Re-review & prescription ────────────────────────────────────────
    public const string S4ReReviewEnabled = "S4ReReviewEnabled";
    public const string NoReReviewRequired = "NoReReviewRequired";
    public const string ReReviewApproved = "ReReviewApproved";
    public const string PrescriptionReferenceExists = "PrescriptionReferenceExists";
    public const string PrescriptionReferenceValid = "PrescriptionReferenceValid";
    public const string FailureEventValid = "FailureEventValid";

    // ── Plan QA ─────────────────────────────────────────────────────────
    public const string PlanAndPrescriptionPresent = "PlanAndPrescriptionPresent";
    public const string QAFormApproved = "QAFormApproved";
    public const string QAFormValid = "QAFormValid";

    // ── Double-check ────────────────────────────────────────────────────
    public const string DoubleCheckApproved = "DoubleCheckApproved";
    public const string S5DoubleCheckEnabled = "S5DoubleCheckEnabled";
    public const string S5DoubleCheckDisabled = "S5DoubleCheckDisabled";

    // ── Scheduling ──────────────────────────────────────────────────────
    public const string ScheduleReferenceExists = "ScheduleReferenceExists";
    public const string ScheduleExists = "ScheduleExists";
    public const string CaseReleasedForSchedule = "CaseReleasedForSchedule";
    public const string SchedulePayloadValid = "SchedulePayloadValid";

    // ── Treatment order & execution ─────────────────────────────────────
    public const string TreatmentOrderFormValid = "TreatmentOrderFormValid";
    public const string QueueOrAppointmentValid = "QueueOrAppointmentValid";
    public const string TreatmentStartEventValid = "TreatmentStartEventValid";
    public const string FractionDataValid = "FractionDataValid";
    public const string PauseReasonProvided = "PauseReasonProvided";
    public const string InterruptionReasonRequired = "InterruptionReasonRequired";
    public const string MedicalApprovalExists = "MedicalApprovalExists";
    public const string TreatmentCompletionSatisfied = "TreatmentCompletionSatisfied";
    public const string S7CompletionRuleSatisfied = "S7CompletionRuleSatisfied";

    // ── Post-treatment & archiving ──────────────────────────────────────
    public const string PostTreatmentReviewFormValid = "PostTreatmentReviewFormValid";
    public const string NoBlockingTasks = "NoBlockingTasks";
    public const string RequiredFormsComplete = "RequiredFormsComplete";
}
