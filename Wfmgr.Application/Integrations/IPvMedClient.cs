namespace Wfmgr.Application.Integrations;

public interface IPvMedClient
{
    Task SendAutoContourAsync(string payloadJson, CancellationToken ct);
    Task QueryContourStatusAsync(string payloadJson, CancellationToken ct);
}
