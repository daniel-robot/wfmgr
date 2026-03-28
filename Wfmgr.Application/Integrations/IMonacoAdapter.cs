namespace Wfmgr.Application.Integrations;

public interface IMonacoAdapter
{
    Task DropImportAsync(Guid caseId, string payloadJson, CancellationToken ct);
}
