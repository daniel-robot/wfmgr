using System.Net.Http.Json;
using Wfmgr.Application.Integrations;

namespace Wfmgr.Infrastructure.Integrations;

public class PvMedClient : IPvMedClient
{
    private readonly HttpClient _httpClient;

    public PvMedClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task SendAutoContourAsync(string payloadJson, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/autocontour/jobs", new { payloadJson }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task QueryContourStatusAsync(string payloadJson, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/autocontour/jobs/status", new { payloadJson }, ct);
        response.EnsureSuccessStatusCode();
    }
}
