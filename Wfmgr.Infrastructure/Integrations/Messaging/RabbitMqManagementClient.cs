using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wfmgr.Infrastructure.Integrations.Messaging;

/// <summary>
/// Thin client over the RabbitMQ management HTTP API. Only used for read-only
/// health probes; no admin operations are exposed here.
/// </summary>
public interface IRabbitMqManagementClient
{
    /// <summary>
    /// Returns every queue in the configured vhost whose name ends in <c>_error</c>
    /// (MassTransit's convention for dead-letter queues). Throws on transport failure.
    /// </summary>
    Task<IReadOnlyList<DeadLetterQueueInfo>> GetDeadLetterQueuesAsync(CancellationToken ct);
}

public sealed record DeadLetterQueueInfo(string Name, long Messages);

public sealed class RabbitMqManagementClient : IRabbitMqManagementClient
{
    private readonly HttpClient _http;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqManagementClient> _logger;

    public RabbitMqManagementClient(
        HttpClient http,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqManagementClient> logger)
    {
        _options = options.Value;
        _logger = logger;

        var baseUri = new UriBuilder(_options.ManagementScheme, _options.Host ?? "localhost", _options.ManagementPort).Uri;
        http.BaseAddress = baseUri;
        var creds = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", creds);
        http.Timeout = TimeSpan.FromSeconds(5);
        _http = http;
    }

    public async Task<IReadOnlyList<DeadLetterQueueInfo>> GetDeadLetterQueuesAsync(CancellationToken ct)
    {
        // Encode the vhost; "/" becomes "%2F" per the management API.
        var vhost = Uri.EscapeDataString(_options.VirtualHost);
        var queues = await _http.GetFromJsonAsync<List<QueueDto>>($"/api/queues/{vhost}", ct).ConfigureAwait(false);
        if (queues is null) return Array.Empty<DeadLetterQueueInfo>();

        var dlqs = new List<DeadLetterQueueInfo>(capacity: queues.Count / 8);
        foreach (var q in queues)
        {
            if (q.Name is not null && q.Name.EndsWith("_error", StringComparison.Ordinal))
            {
                dlqs.Add(new DeadLetterQueueInfo(q.Name, q.Messages));
            }
        }
        _logger.LogDebug("RabbitMQ management probe found {Count} DLQs", dlqs.Count);
        return dlqs;
    }

    private sealed record QueueDto(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("messages")] long Messages);
}
