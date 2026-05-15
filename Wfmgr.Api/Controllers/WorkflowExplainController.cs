using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wfmgr.Api.Auth;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Config;

namespace Wfmgr.Api.Controllers;

/// <summary>
/// Read-only explainability endpoints for the workflow transition catalog.
/// Reuses <see cref="IWorkflowExplainService"/> for dry-run evaluation — no audit,
/// history, or side-effects are written.
/// </summary>
[ApiController]
[Route("api/workflow-config/transitions")]
[Authorize(Policy = WorkflowConfigPolicies.Admin)]
public class WorkflowExplainController : ControllerBase
{
    private readonly IWorkflowExplainService _service;

    public WorkflowExplainController(IWorkflowExplainService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TransitionDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TransitionDefinitionDto>>> GetCatalog(CancellationToken ct)
    {
        var catalog = await _service.GetCatalogAsync(ct);
        return Ok(catalog);
    }

    [HttpPost("explain")]
    [ProducesResponseType(typeof(ExplainTransitionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExplainTransitionResponse>> Explain(
        [FromBody] ExplainTransitionRequest request,
        CancellationToken ct)
    {
        var result = await _service.ExplainAsync(request, ct);
        return Ok(result);
    }
}
