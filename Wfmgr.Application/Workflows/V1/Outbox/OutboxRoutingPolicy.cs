using Microsoft.Extensions.Options;
using Wfmgr.Domain.Integrations;

namespace Wfmgr.Application.Workflows.V1.Outbox;

/// <summary>
/// Decides whether a given outbox action goes via the asynchronous bus (RabbitMQ) or
/// stays on the legacy synchronous HTTP dispatch path. Driven by configuration so an
/// operator can flip a single action without code changes.
/// </summary>
public interface IOutboxRoutingPolicy
{
    OutboxDeliveryMode GetDeliveryMode(string action);
}

public sealed class OutboxRoutingPolicy : IOutboxRoutingPolicy
{
    private readonly HashSet<string> _busActions;

    public OutboxRoutingPolicy(IOptions<MessagingOptions> options)
    {
        _busActions = new HashSet<string>(
            options.Value.BusActions ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    public OutboxDeliveryMode GetDeliveryMode(string action) =>
        _busActions.Contains(action) ? OutboxDeliveryMode.Bus : OutboxDeliveryMode.Http;
}
