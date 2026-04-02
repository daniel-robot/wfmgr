namespace Wfmgr.Application.Workflows.V1.Gates;

/// <summary>
/// Canonical string identifiers for every named gate check used in
/// <see cref="Definitions.TransitionDefinition.GateChecks"/>.
/// <para>
/// Gate check names are matched case-insensitively by <see cref="GateValidationService"/>.
/// All new gate checks should be registered here so that strings in the transition catalog
/// remain in sync with the service implementation.
/// </para>
/// </summary>
public static class GateCheckNames
{
    // ── Simulation ────────────────────────────────────────────────────────────

    /// <summary>A SimulationRequestForm in Submitted status exists for the case.</summary>
    public const string SimulationRequestFormValid = nameof(SimulationRequestFormValid);

    /// <summary>A SimulationRecordForm in Submitted status exists for the case.</summary>
    public const string SimulationRecordFormValid = nameof(SimulationRecordFormValid);

    /// <summary>A SimulationSchedule work item exists for the case.</summary>
    public const string SimulationScheduleExists = nameof(SimulationScheduleExists);

    // ── Case State ────────────────────────────────────────────────────────────

    /// <summary>The case is not in a terminal Cancelled or Archived status.</summary>
    public const string CaseNotCancelled = nameof(CaseNotCancelled);

    /// <summary>Alias: the case is active and not cancelled (same logic as <see cref="CaseNotCancelled"/>).</summary>
    public const string CaseActiveNotCancelled = nameof(CaseActiveNotCancelled);

    /// <summary>
    /// The case is in a pre-treatment status where cancellation is permitted.
    /// Equivalent to checking that treatment has not yet started.
    /// </summary>
    public const string CancellationAllowed = nameof(CancellationAllowed);

    /// <summary>Alias: treatment has not yet started (same logic as <see cref="CancellationAllowed"/>).</summary>
    public const string TreatmentNotStarted = nameof(TreatmentNotStarted);

    // ── Image Acquisition ─────────────────────────────────────────────────────

    /// <summary>Study instance UID and WADO-RS URL are both present on the case record.</summary>
    public const string ImageReferenceExists = nameof(ImageReferenceExists);

    /// <summary>Alias: image references are valid (DICOM UID and WADO-RS URL present).</summary>
    public const string ImageRefsValid = nameof(ImageRefsValid);

    /// <summary>
    /// Image references exist and the WADO-RS endpoint is considered accessible.
    /// In the current implementation this is equivalent to <see cref="ImageReferenceExists"/>;
    /// a live connectivity check would require an additional infrastructure capability.
    /// </summary>
    public const string ImageAccessible = nameof(ImageAccessible);

    /// <summary>
    /// The incoming external event has been correlated to a case via its correlation key.
    /// Validated by requiring a non-empty external event payload in the context.
    /// </summary>
    public const string CaseResolvedByCorrelationKey = nameof(CaseResolvedByCorrelationKey);

    /// <summary>
    /// The external contouring tool has accepted delivery of the forwarded images.
    /// Validated by requiring a non-empty external event payload in the context.
    /// </summary>
    public const string ExternalAcceptOrDeliveryConfirmed = nameof(ExternalAcceptOrDeliveryConfirmed);

    // ── Contouring ────────────────────────────────────────────────────────────

    /// <summary>RTStruct contour result references are recorded on the case.</summary>
    public const string ContourResultExists = nameof(ContourResultExists);

    /// <summary>Alias: contour result references are valid (same logic as <see cref="ContourResultExists"/>).</summary>
    public const string ContourResultRefsValid = nameof(ContourResultRefsValid);

    /// <summary>Revised contour references exist after a rework cycle (same logic as <see cref="ContourResultExists"/>).</summary>
    public const string RevisedContourExists = nameof(RevisedContourExists);

    /// <summary>
    /// The incoming external event payload is valid (non-empty).
    /// Used for progress, failure, and other system events.
    /// </summary>
    public const string EventValid = nameof(EventValid);

    /// <summary>
    /// The incoming external event has not already been processed for this case
    /// (deduplication check via the external events log).
    /// Requires <c>event.source</c>, <c>event.type</c>, and <c>event.externalId</c>
    /// to be present in <see cref="GateValidationContext.Metadata"/>.
    /// </summary>
    public const string EventIdempotent = nameof(EventIdempotent);

    /// <summary>A manual contour payload or form ID is present in the execution context.</summary>
    public const string ManualContourPayloadValid = nameof(ManualContourPayloadValid);

    /// <summary>A retry is permitted for this operation (indicated by context metadata).</summary>
    public const string RetryAllowed = nameof(RetryAllowed);

    // ── Contour Review ────────────────────────────────────────────────────────

    /// <summary>At least one ContourReview work item with result code Approved exists, or a ContourReviewForm has been submitted.</summary>
    public const string ReviewApprovalExists = nameof(ReviewApprovalExists);

    /// <summary>Alias: minimum required approvals have been recorded (same logic as <see cref="ReviewApprovalExists"/>).</summary>
    public const string MinimumApprovalsReached = nameof(MinimumApprovalsReached);

    /// <summary>A non-empty rejection reason is present in the execution context.</summary>
    public const string RejectionReasonRequired = nameof(RejectionReasonRequired);

    // ── Treatment Planning ────────────────────────────────────────────────────

    /// <summary>A plan version exists for the case (either tracked on CaseData or persisted via PlanVersion).</summary>
    public const string PlanVersionExists = nameof(PlanVersionExists);

    /// <summary>A plan design payload or form ID is present in the execution context.</summary>
    public const string PlanPayloadValid = nameof(PlanPayloadValid);

    /// <summary>An assignee user ID is present in the execution context or metadata.</summary>
    public const string AssigneeExists = nameof(AssigneeExists);

    /// <summary>A work item ID is present in the execution context, indicating an active assigned task.</summary>
    public const string TaskAssigned = nameof(TaskAssigned);

    /// <summary>A PlanEvaluation work item with result code Approved exists, or a PlanEvaluationForm has been submitted.</summary>
    public const string PlanEvaluationApproved = nameof(PlanEvaluationApproved);

    /// <summary>Alias: plan evaluation has been approved (same logic as <see cref="PlanEvaluationApproved"/>).</summary>
    public const string EvaluationApproved = nameof(EvaluationApproved);

    /// <summary>A non-empty reason is present in the execution context (generic form of rejection/reason checks).</summary>
    public const string ReasonRequired = nameof(ReasonRequired);

    /// <summary>Alias: a failure reason is required (same logic as <see cref="ReasonRequired"/>).</summary>
    public const string FailureReasonRequired = nameof(FailureReasonRequired);

    /// <summary>Alias: a rework decision (reason) has been provided (same logic as <see cref="ReasonRequired"/>).</summary>
    public const string ReworkDecisionMade = nameof(ReworkDecisionMade);

    // ── Re-review &amp; Prescription ──────────────────────────────────────────

    /// <summary>
    /// The S4 plan re-review policy slot is enabled for this case's organisation context.
    /// Resolved via <see cref="IWorkflowProfileResolver"/>.
    /// </summary>
    public const string S4ReReviewEnabled = nameof(S4ReReviewEnabled);

    /// <summary>
    /// The S4 plan re-review policy slot is disabled for this case (no re-review required).
    /// Resolved via <see cref="IWorkflowProfileResolver"/>.
    /// </summary>
    public const string NoReReviewRequired = nameof(NoReReviewRequired);

    /// <summary>A PlanReReview work item with result code Approved exists, or a PlanReReviewForm has been submitted.</summary>
    public const string ReReviewApproved = nameof(ReReviewApproved);

    /// <summary>A PrescriptionSync work item with result code Synced exists, confirming prescription availability.</summary>
    public const string PrescriptionReferenceExists = nameof(PrescriptionReferenceExists);

    /// <summary>Alias: prescription reference is valid (same logic as <see cref="PrescriptionReferenceExists"/>).</summary>
    public const string PrescriptionReferenceValid = nameof(PrescriptionReferenceValid);

    /// <summary>The incoming failure event payload is non-empty and parseable.</summary>
    public const string FailureEventValid = nameof(FailureEventValid);

    // ── Plan QA ───────────────────────────────────────────────────────────────

    /// <summary>Both a plan version and a prescription reference exist for the case.</summary>
    public const string PlanAndPrescriptionPresent = nameof(PlanAndPrescriptionPresent);

    /// <summary>A PlanQA work item with result code Approved exists, or a PlanQAForm has been submitted.</summary>
    public const string QAFormApproved = nameof(QAFormApproved);

    /// <summary>Alias: QA form has been submitted and approved (same logic as <see cref="QAFormApproved"/>).</summary>
    public const string QAFormValid = nameof(QAFormValid);

    // ── Plan Double-check ─────────────────────────────────────────────────────

    /// <summary>A PlanDoubleCheck work item with result code Approved exists, or a PlanDoubleCheckForm has been submitted.</summary>
    public const string DoubleCheckApproved = nameof(DoubleCheckApproved);

    /// <summary>
    /// The S5 double-check policy slot is enabled for this case's organisation context.
    /// Resolved via <see cref="IWorkflowProfileResolver"/>.
    /// </summary>
    public const string S5DoubleCheckEnabled = nameof(S5DoubleCheckEnabled);

    /// <summary>
    /// The S5 double-check policy slot is disabled for this case (double-check not required).
    /// Resolved via <see cref="IWorkflowProfileResolver"/>.
    /// </summary>
    public const string S5DoubleCheckDisabled = nameof(S5DoubleCheckDisabled);

    // ── Scheduling ────────────────────────────────────────────────────────────

    /// <summary>A ScheduleSync work item with result code Synced exists, confirming a valid schedule reference.</summary>
    public const string ScheduleReferenceExists = nameof(ScheduleReferenceExists);

    /// <summary>Alias: a schedule reference exists (same logic as <see cref="ScheduleReferenceExists"/>).</summary>
    public const string ScheduleExists = nameof(ScheduleExists);

    /// <summary>Alias: case has been released for scheduling (same logic as <see cref="ScheduleReferenceExists"/>).</summary>
    public const string CaseReleasedForSchedule = nameof(CaseReleasedForSchedule);

    /// <summary>The scheduling payload in the external event context is non-empty.</summary>
    public const string SchedulePayloadValid = nameof(SchedulePayloadValid);

    // ── Treatment Order &amp; Execution ────────────────────────────────────────

    /// <summary>A TreatmentOrderForm in Submitted status exists, or a TreatmentOrder work item with result code Submitted exists.</summary>
    public const string TreatmentOrderFormValid = nameof(TreatmentOrderFormValid);

    /// <summary>Queue or appointment data is present in the external event payload.</summary>
    public const string QueueOrAppointmentValid = nameof(QueueOrAppointmentValid);

    /// <summary>Treatment start event payload is non-empty.</summary>
    public const string TreatmentStartEventValid = nameof(TreatmentStartEventValid);

    /// <summary>Treatment fraction data payload is non-empty.</summary>
    public const string FractionDataValid = nameof(FractionDataValid);

    /// <summary>A non-empty pause reason is present in the execution context.</summary>
    public const string PauseReasonProvided = nameof(PauseReasonProvided);

    /// <summary>The case is in a resumable paused state and resume is clinically permitted.</summary>
    public const string ResumeAllowed = nameof(ResumeAllowed);

    /// <summary>An interruption reason is present in the execution context.</summary>
    public const string InterruptionReasonRequired = nameof(InterruptionReasonRequired);

    /// <summary>Medical clearance or approval for resuming after interruption exists.</summary>
    public const string MedicalApprovalExists = nameof(MedicalApprovalExists);

    /// <summary>Treatment completion criteria have been satisfied per the S7 completion policy.</summary>
    public const string TreatmentCompletionSatisfied = nameof(TreatmentCompletionSatisfied);

    /// <summary>Alias: S7 treatment completion rule is satisfied (same logic as <see cref="TreatmentCompletionSatisfied"/>).</summary>
    public const string S7CompletionRuleSatisfied = nameof(S7CompletionRuleSatisfied);

    /// <summary>The case is currently in <c>TreatmentCompleted</c> status.</summary>
    public const string TreatmentCompleted = nameof(TreatmentCompleted);

    // ── Post-treatment &amp; Archiving ─────────────────────────────────────────

    /// <summary>A PostTreatmentReviewForm in Submitted status exists, or a PostTreatmentReview work item with result code Reviewed exists.</summary>
    public const string PostTreatmentReviewFormValid = nameof(PostTreatmentReviewFormValid);

    /// <summary>No work items for the case are in a blocking (Pending or InProgress) state.</summary>
    public const string NoBlockingTasks = nameof(NoBlockingTasks);

    /// <summary>All required forms for archiving have been submitted (validated as a subset of post-treatment review).</summary>
    public const string RequiredFormsComplete = nameof(RequiredFormsComplete);

    // ── Metadata keys for EventIdempotent check ───────────────────────────────

    /// <summary>
    /// <see cref="GateValidationContext.Metadata"/> key for the external event source system name.
    /// Required by the <see cref="EventIdempotent"/> gate check.
    /// </summary>
    public const string MetaEventSource = "event.source";

    /// <summary>
    /// <see cref="GateValidationContext.Metadata"/> key for the external event type string.
    /// Required by the <see cref="EventIdempotent"/> gate check.
    /// </summary>
    public const string MetaEventType = "event.type";

    /// <summary>
    /// <see cref="GateValidationContext.Metadata"/> key for the external event's unique identifier.
    /// Required by the <see cref="EventIdempotent"/> gate check.
    /// </summary>
    public const string MetaEventExternalId = "event.externalId";

    /// <summary>
    /// <see cref="GateValidationContext.Metadata"/> key for the assignee user ID.
    /// Required by the <see cref="AssigneeExists"/> gate check.
    /// </summary>
    public const string MetaAssigneeUserId = "assigneeUserId";

    /// <summary>
    /// <see cref="GateValidationContext.Metadata"/> key for indicating whether a retry is permitted.
    /// Expected value: <c>true</c> (bool). Required by the <see cref="RetryAllowed"/> gate check.
    /// </summary>
    public const string MetaRetryAllowed = "retryAllowed";
}
