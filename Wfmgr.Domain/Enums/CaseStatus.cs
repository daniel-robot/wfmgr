namespace Wfmgr.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a radiotherapy workflow case.
/// Values are spaced by 10 to allow future intermediate states without renumbering.
/// </summary>
public enum CaseStatus
{

    /// <summary>Case has been submitted, the workflow started.</summary>
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

    // ── Contouring (granular) ────────────────────────────────────────────────
    /// <summary>Third-party auto-contouring (e.g. PvMed) is currently running.</summary>
    AutoContouringInProgress = 52,
    /// <summary>Third-party auto-contouring has finished and RTStruct refs are available.</summary>
    AutoContouringCompleted = 54,
    /// <summary>Manual contouring touch-up / fallback is in progress.</summary>
    ManualContouringInProgress = 56,
    /// <summary>Manual contouring is complete; ready to be promoted to <see cref="ContoursReady"/>.</summary>
    ManualContouringCompleted = 58,

    // ── Contouring (legacy) ──────────────────────────────────────────────────
    /// <summary>Legacy single-bucket contouring state. Retained for backward compatibility.</summary>
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

    // ── Plan QA ───────────────────────────────────────────────────────────────
    /// <summary>Physics QA verification of the plan is in progress.</summary>
    PlanQAInProgress = 220,
    /// <summary>Plan has passed QA verification.</summary>
    PlanQAApproved = 230,
    /// <summary>Plan did not pass QA and requires remediation.</summary>
    PlanQAFailed = 240,
    /// <summary>An optional second independent check of the plan may be performed.</summary>
    PlanDoubleCheckOptional = 250,

    /// <summary>Case has been cancelled and will not proceed further.</summary>
    Cancelled = 390
}
