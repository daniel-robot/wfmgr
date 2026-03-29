namespace Wfmgr.Domain.Integrations;

public static class ExternalIntegrationEventTypes
{
    public const string CtImageStored = "CT_IMAGE_STORED";
    public const string CtImageStorageFailed = "CT_IMAGE_STORAGE_FAILED";

    public const string AutoContourStarted = "AUTOCONTOUR_STARTED";
    public const string AutoContourProgress = "AUTOCONTOUR_PROGRESS";
    public const string AutoContourCompleted = "AUTOCONTOUR_COMPLETED";
    public const string AutoContourFailed = "AUTOCONTOUR_FAILED";
    public const string ManualContourCompleted = "MANUAL_CONTOUR_COMPLETED";

    public const string MonacoImportAccepted = "MONACO_IMPORT_ACCEPTED";
    public const string MonacoImportFailed = "MONACO_IMPORT_FAILED";
    public const string PlanCreated = "PLAN_CREATED";
    public const string PlanUpdated = "PLAN_UPDATED";
    public const string PlanReviewCompleted = "PLAN_REVIEW_COMPLETED";
    public const string PlanReviewFailed = "PLAN_REVIEW_FAILED";

    public const string PrescriptionGenerated = "PRESCRIPTION_GENERATED";
    public const string PrescriptionSyncFailed = "PRESCRIPTION_SYNC_FAILED";
    public const string ScheduleSynced = "SCHEDULE_SYNCED";
    public const string TreatmentStarted = "TREATMENT_STARTED";
    public const string TreatmentFractionCompleted = "TREATMENT_FRACTION_COMPLETED";
    public const string TreatmentCourseCompleted = "TREATMENT_COURSE_COMPLETED";
    public const string TreatmentInterrupted = "TREATMENT_INTERRUPTED";
}
