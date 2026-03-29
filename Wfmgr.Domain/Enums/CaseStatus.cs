namespace Wfmgr.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a radiotherapy workflow case.
/// Values are spaced by 10 to allow future intermediate states without renumbering.
/// </summary>
public enum CaseStatus
{
    // ── Intake ────────────────────────────────────────────────────────────────
    /// <summary>Case has been created but not yet submitted for processing.</summary>
    Draft = 0,
    /// <summary>Case has been submitted and is awaiting simulation scheduling.</summary>
    Submitted = 10,

    // ── Simulation ────────────────────────────────────────────────────────────
    /// <summary>CT simulation appointment has been booked.</summary>
    SimScheduled = 20,
    /// <summary>CT simulation scan is currently in progress.</summary>
    SimInProgress = 30,
    /// <summary>CT simulation has been completed and the record submitted.</summary>
    SimCompleted = 40,

    // ── Image Acquisition ────────────────────────────────────────────────────
    /// <summary>CT images have been stored in the DICOM repository.</summary>
    ImageStored = 50,
    /// <summary>CT images are being forwarded to the contouring system.</summary>
    ImageForwarding = 60,

    // ── Contouring ────────────────────────────────────────────────────────────
    /// <summary>Auto-contouring or manual contouring is underway.</summary>
    ContouringInProgress = 70,
    /// <summary>Contours have been generated and are ready for review.</summary>
    ContoursReady = 80,
    /// <summary>Contours are currently under clinical review.</summary>
    ContoursUnderReview = 90,
    /// <summary>Contours were rejected during review and need rework.</summary>
    ContoursRejected = 100,
    /// <summary>Contour rework has been formally requested.</summary>
    ContourReworkRequired = 110,

    // ── Planning ──────────────────────────────────────────────────────────────
    /// <summary>Contours have been forwarded to the planning system; planning not yet started.</summary>
    PlanningPending = 120,
    /// <summary>A dosimetrist has been assigned to create the treatment plan.</summary>
    PlanningAssigned = 130,
    /// <summary>Treatment plan is actively being created.</summary>
    PlanningInProgress = 140,
    /// <summary>Treatment plan has been completed and is ready for review.</summary>
    PlanReady = 150,
    /// <summary>Treatment plan is currently under physician review.</summary>
    PlanUnderReview = 160,
    /// <summary>Treatment plan has been reviewed and approved.</summary>
    PlanReviewed = 170,
    /// <summary>An optional secondary review of the plan may be requested.</summary>
    PlanReReviewOptional = 180,

    // ── Prescription ──────────────────────────────────────────────────────────
    /// <summary>Prescription document is being generated from the approved plan.</summary>
    PrescriptionGenerating = 190,
    /// <summary>Prescription has been generated and is ready for use.</summary>
    PrescriptionReady = 200,
    /// <summary>Prescription synchronisation to the oncology system failed.</summary>
    PrescriptionSyncFailed = 210,

    // ── Plan QA ───────────────────────────────────────────────────────────────
    /// <summary>Physics QA verification of the plan is in progress.</summary>
    PlanQAInProgress = 220,
    /// <summary>Plan has passed QA verification.</summary>
    PlanQAApproved = 230,
    /// <summary>Plan did not pass QA and requires remediation.</summary>
    PlanQAFailed = 240,
    /// <summary>An optional second independent check of the plan may be performed.</summary>
    PlanDoubleCheckOptional = 250,

    // ── Scheduling ────────────────────────────────────────────────────────────
    /// <summary>All plan approvals are complete; case is ready to be scheduled.</summary>
    ReadyForScheduling = 260,
    /// <summary>Treatment fractions are being scheduled.</summary>
    SchedulingInProgress = 270,
    /// <summary>Treatment sessions have been scheduled.</summary>
    Scheduled = 280,

    // ── Order / Queue ─────────────────────────────────────────────────────────
    /// <summary>Treatment order is being prepared for submission to the treatment system.</summary>
    OrderPending = 290,
    /// <summary>Treatment order has been submitted to the treatment management system.</summary>
    OrderSubmitted = 300,
    /// <summary>Case is queued in the linac/treatment unit queue.</summary>
    QueuePending = 310,

    // ── Treatment ─────────────────────────────────────────────────────────────
    /// <summary>Patient is actively receiving treatment.</summary>
    Treating = 320,
    /// <summary>Treatment has been temporarily paused (e.g. setup adjustment).</summary>
    TreatmentPaused = 330,
    /// <summary>Treatment was interrupted and requires clinical review before resuming.</summary>
    TreatmentInterrupted = 340,
    /// <summary>All treatment fractions have been delivered.</summary>
    TreatmentCompleted = 350,

    // ── Post-Treatment ────────────────────────────────────────────────────────
    /// <summary>Post-treatment clinical review is pending.</summary>
    PostTreatmentReviewPending = 360,
    /// <summary>Post-treatment review has been completed.</summary>
    PostTreatmentReviewed = 370,

    // ── Terminal ──────────────────────────────────────────────────────────────
    /// <summary>Case has been archived after completion of all workflow steps.</summary>
    Archived = 380,
    /// <summary>Case has been cancelled and will not proceed further.</summary>
    Cancelled = 390
}
