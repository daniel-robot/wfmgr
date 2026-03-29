using Microsoft.AspNetCore.Mvc;
using Wfmgr.Application.Integrations;
using Wfmgr.Application.Integrations.Dtos;

namespace Wfmgr.Api.Controllers;

[ApiController]
[Route("api/integration/events")]
public class ExternalEventsController : ControllerBase
{
    private readonly IExternalEventDispatcher _dispatcher;

    public ExternalEventsController(IExternalEventDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpPost]
    public async Task<IActionResult> Dispatch([FromBody] ExternalIntegrationEventRequest request, CancellationToken ct)
    {
        await _dispatcher.DispatchAsync(request, ct);
        return Accepted();
    }
}
