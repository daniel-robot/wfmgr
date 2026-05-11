using Microsoft.AspNetCore.Mvc;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Dtos;

namespace Wfmgr.Api.Controllers;

[ApiController]
[Route("api/integration/pvmed")]
// TODO: Add API key or mTLS authentication for external system callbacks before production.
public class PvMedIntegrationController : ControllerBase
{
    private readonly ICaseWorkflowService _workflowService;

    public PvMedIntegrationController(ICaseWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    [HttpPost("events")]
    public async Task<IActionResult> HandlePvMedEvent([FromBody] PvMedEventRequest request, CancellationToken ct)
    {
        await _workflowService.HandlePvMedEventAsync(request, ct);
        return Accepted();
    }
}
