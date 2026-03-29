namespace Wfmgr.Domain.WorkItems;

public static class WorkItemTypes
{
    public const string SimulationRequest = nameof(SimulationRequest);
    public const string SimulationSchedule = nameof(SimulationSchedule);
    public const string SimulationRecord = nameof(SimulationRecord);

    public const string ImageValidation = nameof(ImageValidation);
    public const string ImageForwardToContourTool = nameof(ImageForwardToContourTool);
    public const string AutoContourMonitor = nameof(AutoContourMonitor);
    public const string ManualContouring = nameof(ManualContouring);
    public const string ContourReview = nameof(ContourReview);
    public const string ContourSecondReview = nameof(ContourSecondReview);
    public const string ContourRework = nameof(ContourRework);

    public const string PlanAssignment = nameof(PlanAssignment);
    public const string PlanDesign = nameof(PlanDesign);
    public const string PlanEvaluation = nameof(PlanEvaluation);
    public const string PlanReReview = nameof(PlanReReview);

    public const string PrescriptionSync = nameof(PrescriptionSync);
    public const string PlanQA = nameof(PlanQA);
    public const string PlanDoubleCheck = nameof(PlanDoubleCheck);

    public const string ScheduleSync = nameof(ScheduleSync);
    public const string TreatmentOrder = nameof(TreatmentOrder);
    public const string QueueCall = nameof(QueueCall);
    public const string TreatmentMonitor = nameof(TreatmentMonitor);
    public const string TreatmentExceptionHandling = nameof(TreatmentExceptionHandling);

    public const string PostTreatmentReview = nameof(PostTreatmentReview);
    public const string ArchiveReview = nameof(ArchiveReview);

    public const string ManualForwardToMonaco = nameof(ManualForwardToMonaco);
}
