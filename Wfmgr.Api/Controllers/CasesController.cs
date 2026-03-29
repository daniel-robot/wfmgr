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

    [HttpPost("{caseId:guid}/actions/restart-contouring")]
    public async Task<IActionResult> RestartContouring(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        await _workflowService.RestartContouringAsync(caseId, request.Reason ?? "Manual restart", request.TriggeredBy, ct);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/reject-contour-review")]
    public async Task<IActionResult> RejectContourReview(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        await _workflowService.RejectContourReviewAsync(caseId, request.Reason ?? "Manual rejection", request.TriggeredBy, ct);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/reject-plan-review")]
    public async Task<IActionResult> RejectPlanReview(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        await _workflowService.RejectPlanReviewAsync(caseId, request.Reason ?? "Manual rejection", request.TriggeredBy, ct);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/reject-plan-rereview")]
    public async Task<IActionResult> RejectPlanReReview(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        await _workflowService.RejectPlanReReviewAsync(caseId, request.Reason ?? "Manual rejection", request.TriggeredBy, ct);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/prescription-sync-failed")]
    public async Task<IActionResult> HandlePrescriptionSyncFailure(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        await _workflowService.HandlePrescriptionSyncFailureAsync(caseId, request.Reason ?? "Manual failure", request.TriggeredBy, ct);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/retry-prescription-sync")]
    public async Task<IActionResult> RetryPrescriptionSync(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        await _workflowService.RetryPrescriptionSyncAsync(caseId, request.TriggeredBy, ct);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/resolve-prescription-sync")]
    public async Task<IActionResult> ResolvePrescriptionSync(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        await _workflowService.ResolvePrescriptionSyncAsync(caseId, request.TriggeredBy, ct);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/fail-qa")]
    public async Task<IActionResult> FailQa(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        await _workflowService.FailQaAsync(caseId, request.Reason ?? "Manual failure", request.TriggeredBy, ct);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/scheduling-failed")]
    public async Task<IActionResult> HandleSchedulingFailure(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        await _workflowService.HandleSchedulingFailureAsync(caseId, request.Reason ?? "Manual scheduling failure", request.TriggeredBy, ct);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/retry-scheduling")]
    public async Task<IActionResult> RetryScheduling(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        await _workflowService.RetrySchedulingAsync(caseId, request.TriggeredBy, ct);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/pause-treatment")]
    public async Task<IActionResult> PauseTreatment(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        await _workflowService.PauseTreatmentAsync(caseId, request.Reason ?? "Manual pause", request.TriggeredBy, ct);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/interrupt-treatment")]
    public async Task<IActionResult> InterruptTreatment(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        await _workflowService.InterruptTreatmentAsync(caseId, request.Reason ?? "Manual interruption", request.TriggeredBy, ct);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/resume-treatment")]
    public async Task<IActionResult> ResumeTreatment(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        await _workflowService.ResumeTreatmentAsync(caseId, request.TriggeredBy, ct);
        return NoContent();
    }

    [HttpPost("{caseId:guid}/actions/cancel")]
    public async Task<IActionResult> CancelCase(Guid caseId, [FromBody] WorkflowActionRequest request, CancellationToken ct)
    {
        await _workflowService.CancelCaseAsync(caseId, request.Reason ?? "Manual cancellation", request.TriggeredBy, ct);
        return NoContent();
    }
}
