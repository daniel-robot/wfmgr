using System.Net.Http.Json;
using Wfmgr.Application.Integrations;

namespace Wfmgr.Infrastructure.Integrations;

public class MsqClient : IMsqClient
{
    private readonly HttpClient _httpClient;

    public MsqClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task GeneratePrescriptionAsync(string payloadJson, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/prescriptions/generate", new { payloadJson }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SyncScheduleAsync(string payloadJson, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/schedules/sync", new { payloadJson }, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task QueryTreatmentProgressAsync(string payloadJson, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/treatment/progress/query", new { payloadJson }, ct);
        response.EnsureSuccessStatusCode();
    }
}
