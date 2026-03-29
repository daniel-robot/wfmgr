using Microsoft.AspNetCore.Mvc;
using Wfmgr.Application.Workflows.V1;

namespace Wfmgr.Api.Controllers;

[ApiController]
[Route("api/workflow")]
public class WorkflowController : ControllerBase
{
    private readonly ICaseQueryService _queryService;

    public WorkflowController(ICaseQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet("statuses")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowOptionDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<WorkflowOptionDto>> GetStatuses()
    {
        return Ok(_queryService.GetWorkflowStatuses());
    }

    [HttpGet("work-item-types")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowOptionDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<WorkflowOptionDto>> GetWorkItemTypes()
    {
        return Ok(_queryService.GetWorkflowWorkItemTypes());
    }
}
