using Microsoft.AspNetCore.Mvc;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Dtos;

namespace Wfmgr.Api.Controllers;

[ApiController]
[Route("api/integration/pvmed")]
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
