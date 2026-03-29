namespace Wfmgr.Application.Integrations;

public interface IMsqClient
{
    Task GeneratePrescriptionAsync(string payloadJson, CancellationToken ct);
    Task SyncScheduleAsync(string payloadJson, CancellationToken ct);
    Task QueryTreatmentProgressAsync(string payloadJson, CancellationToken ct);
}
