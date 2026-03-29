namespace Wfmgr.Application.Integrations;

public interface IMonacoAdapter
{
    Task SendToMonacoImportAsync(Guid caseId, string payloadJson, CancellationToken ct);
    Task DropImportAsync(Guid caseId, string payloadJson, CancellationToken ct);
}
