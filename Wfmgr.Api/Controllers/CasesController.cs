using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wfmgr.Api.Auth;
using Wfmgr.Application.Abstractions;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Dtos;

namespace Wfmgr.Api.Controllers;

[ApiController]
[Route("api/cases")]
[Authorize]
public class CasesController : ControllerBase
{
    private readonly ICaseWorkflowService _workflowService;
    private readonly ICaseQueryService _queryService;
    private readonly IActorAccessor _actorAccessor;

    public CasesController(
        ICaseWorkflowService workflowService,
        ICaseQueryService queryService,
        IActorAccessor actorAccessor)
    {
        _workflowService = workflowService;
        _queryService = queryService;
        _actorAccessor = actorAccessor;
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

    [HttpGet("{caseId:guid}/transition-history")]
    [ProducesResponseType(typeof(IReadOnlyList<TransitionHistoryViewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TransitionHistoryViewDto>>> GetTransitionHistory(Guid caseId, CancellationToken ct)
    {
        var items = await _queryService.GetTransitionHistoryByCaseIdAsync(caseId, ct);
        return Ok(items);
    }

    [HttpGet("{caseId:guid}/attachments")]
    [ProducesResponseType(typeof(IReadOnlyList<CaseAttachmentViewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CaseAttachmentViewDto>>> GetAttachments(Guid caseId, CancellationToken ct)
    {
        var items = await _queryService.GetAttachmentsByCaseIdAsync(caseId, ct);
        return Ok(items);
    }

    [HttpGet("{caseId:guid}/external-events")]
    [ProducesResponseType(typeof(IReadOnlyList<ExternalEventViewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExternalEventViewDto>>> GetExternalEvents(Guid caseId, CancellationToken ct)
    {
        var items = await _queryService.GetExternalEventsByCaseIdAsync(caseId, ct);
        return Ok(items);
    }

    [HttpGet("{caseId:guid}/integration-references")]
    [ProducesResponseType(typeof(IReadOnlyList<IntegrationReferenceViewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IntegrationReferenceViewDto>>> GetIntegrationReferences(Guid caseId, CancellationToken ct)
    {
        var items = await _queryService.GetIntegrationReferencesByCaseIdAsync(caseId, ct);
        return Ok(items);
    }

    [HttpGet("{caseId:guid}/plan-versions")]
    [ProducesResponseType(typeof(IReadOnlyList<PlanVersionViewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlanVersionViewDto>>> GetPlanVersions(Guid caseId, CancellationToken ct)
    {
        var items = await _queryService.GetPlanVersionsByCaseIdAsync(caseId, ct);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<object>> CreateCase([FromBody] CreateCaseRequest request, CancellationToken ct)
    {
        var caseId = await _workflowService.CreateCaseAsync(request, ct);
        return CreatedAtAction(nameof(CreateCase), new { caseId }, new { caseId });
    }

    [HttpPost("{caseId:guid}/actions/complete-daily-image-scan")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteDailyImageScan(
        Guid caseId,
        [FromBody] WorkflowActionRequest? request,
        CancellationToken ct)
    {
        try
        {
            var actor = _actorAccessor.Current;
            await _workflowService.CompleteDailyImageScanAsync(caseId, request?.TriggeredBy, ct, actor.Roles);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{caseId}/forward/monaco")]
    public async Task<IActionResult> ForwardToMonaco(Guid caseId, CancellationToken ct)
    {
        await _workflowService.ForwardToMonacoAsync(caseId, ct);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/complete-manual-contouring")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteManualContouring(Guid caseId, CancellationToken ct)
    {
        try
        {
            var actor = _actorAccessor.Current;
            await _workflowService.CompleteManualContouringAsync(caseId, ct, actor.Roles);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("RoleDenied", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("requires one of roles", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{caseId:guid}/actions/reject-plan-review")]
    public async Task<IActionResult> RejectPlanReview(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        var actor = _actorAccessor.Current;
        await _workflowService.RejectPlanReviewAsync(caseId, request.Reason ?? "Manual rejection", request.TriggeredBy, ct, actor.Roles);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/reject-plan-rereview")]
    public async Task<IActionResult> RejectPlanReReview(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        var actor = _actorAccessor.Current;
        await _workflowService.RejectPlanReReviewAsync(caseId, request.Reason ?? "Manual rejection", request.TriggeredBy, ct, actor.Roles);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/fail-qa")]
    public async Task<IActionResult> FailQa(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        var actor = _actorAccessor.Current;
        await _workflowService.FailQaAsync(caseId, request.Reason ?? "Manual failure", request.TriggeredBy, ct, actor.Roles);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/cancel")]
    public async Task<IActionResult> CancelCase(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        var actor = _actorAccessor.Current;
        await _workflowService.CancelCaseAsync(caseId, request.Reason ?? "Manual cancellation", request.TriggeredBy, ct, actor.Roles);
        return NoContent();
    }
}
