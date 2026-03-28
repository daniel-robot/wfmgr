using Microsoft.AspNetCore.Mvc;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Dtos;

namespace Wfmgr.Api.Controllers;

[ApiController]
[Route("api/integration/ct")]
public class CtIntegrationController : ControllerBase
{
    private readonly ICaseWorkflowService _workflowService;

    public CtIntegrationController(ICaseWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    [HttpPost("image-stored")]
    public async Task<IActionResult> HandleImageStored([FromBody] CtImageStoredRequest request, CancellationToken ct)
    {
        await _workflowService.HandleCtImageStoredAsync(request, ct);
        return Accepted();
    }
}
