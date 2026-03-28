using Microsoft.AspNetCore.Mvc;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Dtos;

namespace Wfmgr.Api.Controllers;

[ApiController]
[Route("api/cases")]
public class CasesController : ControllerBase
{
    private readonly ICaseWorkflowService _workflowService;
    private readonly ICaseQueryService _queryService;

    public CasesController(ICaseWorkflowService workflowService, ICaseQueryService queryService)
    {
        _workflowService = workflowService;
        _queryService = queryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CaseListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CaseListItemDto>>> GetCases(CancellationToken ct)
    {
        var items = await _queryService.GetCasesAsync(ct);
        return Ok(items);
    }

    [HttpGet("{caseId:guid}")]
    [ProducesResponseType(typeof(CaseDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaseDetailsDto>> GetCase(Guid caseId, CancellationToken ct)
    {
        var item = await _queryService.GetCaseByIdAsync(caseId, ct);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpGet("{caseId:guid}/work-items")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkItemViewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WorkItemViewDto>>> GetWorkItems(Guid caseId, CancellationToken ct)
    {
        var items = await _queryService.GetWorkItemsByCaseIdAsync(caseId, ct);
        return Ok(items);
    }

    [HttpGet("{caseId:guid}/audit-logs")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogViewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuditLogViewDto>>> GetAuditLogs(Guid caseId, CancellationToken ct)
    {
        var items = await _queryService.GetAuditLogsByCaseIdAsync(caseId, ct);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<object>> CreateCase([FromBody] CreateCaseRequest request, CancellationToken ct)
    {
        var caseId = await _workflowService.CreateCaseAsync(request, ct);
        return CreatedAtAction(nameof(CreateCase), new { caseId }, new { caseId });
    }

    [HttpPost("{caseId:guid}/sim-record")]
    public async Task<IActionResult> SubmitSimRecord(Guid caseId, [FromBody] SubmitSimRecordRequest request, CancellationToken ct)
    {
        await _workflowService.SubmitSimRecordAsync(caseId, request, ct);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/forward/monaco")]
    public async Task<IActionResult> ForwardToMonaco(Guid caseId, CancellationToken ct)
    {
        await _workflowService.ForwardToMonacoAsync(caseId, ct);
        return NoContent();
    }
}
