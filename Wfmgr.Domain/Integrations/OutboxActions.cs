namespace Wfmgr.Domain.Integrations;

public static class OutboxActions
{
    public const string SendImagesToContourTool = nameof(SendImagesToContourTool);
    public const string SendToMonacoImport = nameof(SendToMonacoImport);
    public const string QueryContourStatus = nameof(QueryContourStatus);
    public const string GeneratePrescription = nameof(GeneratePrescription);
    public const string SyncSchedule = nameof(SyncSchedule);
    public const string QueryTreatmentProgress = nameof(QueryTreatmentProgress);
}
