using Wfmgr.Application.Integrations.Dtos;

namespace Wfmgr.Application.Integrations;

public interface IExternalEventDispatcher
{
    Task DispatchAsync(ExternalIntegrationEventRequest request, CancellationToken ct);
}
