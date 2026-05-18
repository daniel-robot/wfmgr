using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wfmgr.Api.Auth;
using Wfmgr.Application.Workflows.V1.Config;
using Wfmgr.Application.Workflows.V1.Definitions;

namespace Wfmgr.Api.Controllers;

/// <summary>
/// Admin endpoints for managing the DB-backed workflow transition catalog.
/// All endpoints require the same admin permission as the workflow-config endpoints.
/// </summary>
[ApiController]
[Route("api/workflow-transitions")]
[Authorize(Policy = WorkflowConfigPolicies.Admin)]
public class WorkflowTransitionsController : ControllerBase
{
    private readonly IWorkflowTransitionCatalogService _service;

    public WorkflowTransitionsController(IWorkflowTransitionCatalogService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowTransitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WorkflowTransitionDto>>> List(CancellationToken ct)
    {
        var items = await _service.ListAllAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkflowTransitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowTransitionDto>> Get(Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("by-code/{code}")]
    [ProducesResponseType(typeof(WorkflowTransitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowTransitionDto>> GetByCode(string code, CancellationToken ct)
    {
        var item = await _service.GetByCodeAsync(code, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(WorkflowTransitionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidateWorkflowTransitionResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowTransitionDto>> Create(
        [FromBody] CreateWorkflowTransitionRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, GetActorId(), ct);
        if (result.IsValidationError) return BadRequest(result.ValidationError);
        if (result.IsConflict) return Conflict(result.Conflict);
        return CreatedAtAction(nameof(Get), new { id = result.Transition!.Id }, result.Transition);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(WorkflowTransitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidateWorkflowTransitionResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(WorkflowTransitionMutationConflictDto), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowTransitionDto>> Update(
        Guid id,
        [FromBody] UpdateWorkflowTransitionRequest request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, GetActorId(), ct);
        if (result.NotFound) return NotFound();
        if (result.IsValidationError) return BadRequest(result.ValidationError);
        if (result.IsConflict) return Conflict(result.Conflict);
        return Ok(result.Transition);
    }

    [HttpPost("{id:guid}/enable")]
    [ProducesResponseType(typeof(WorkflowTransitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(WorkflowTransitionMutationConflictDto), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowTransitionDto>> Enable(
        Guid id,
        [FromBody] ToggleWorkflowTransitionRequest? request,
        CancellationToken ct)
    {
        request ??= new ToggleWorkflowTransitionRequest(null, null);
        var result = await _service.SetEnabledAsync(id, true, request, GetActorId(), ct);
        if (result.NotFound) return NotFound();
        if (result.IsConflict) return Conflict(result.Conflict);
        return Ok(result.Transition);
    }

    [HttpPost("{id:guid}/disable")]
    [ProducesResponseType(typeof(WorkflowTransitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(WorkflowTransitionMutationConflictDto), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowTransitionDto>> Disable(
        Guid id,
        [FromBody] ToggleWorkflowTransitionRequest? request,
        CancellationToken ct)
    {
        request ??= new ToggleWorkflowTransitionRequest(null, null);
        var result = await _service.SetEnabledAsync(id, false, request, GetActorId(), ct);
        if (result.NotFound) return NotFound();
        if (result.IsConflict) return Conflict(result.Conflict);
        return Ok(result.Transition);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(WorkflowTransitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(WorkflowTransitionMutationConflictDto), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowTransitionDto>> Delete(
        Guid id,
        [FromQuery] string? expectedHash,
        [FromQuery] string? changeReason,
        CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, new ToggleWorkflowTransitionRequest(expectedHash, changeReason), GetActorId(), ct);
        if (result.NotFound) return NotFound();
        if (result.IsConflict) return Conflict(result.Conflict);
        return Ok(result.Transition);
    }

    [HttpPost("validate")]
    [ProducesResponseType(typeof(ValidateWorkflowTransitionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ValidateWorkflowTransitionResponse>> Validate(
        [FromBody] CreateWorkflowTransitionRequest request,
        CancellationToken ct)
    {
        var result = await _service.ValidateAsync(
            request.Code, request.ToStatus, request.TriggerType, request.FromStatuses,
            request.RequiredRoles, request.GateChecks, request.SuccessActions,
            request.FailureActions, request.WorkItemsToCreate, request.ConfigSlot, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/changelog")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowTransitionChangeLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<WorkflowTransitionChangeLogDto>>> GetChangeLog(
        Guid id,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var existing = await _service.GetByIdAsync(id, ct);
        if (existing is null) return NotFound();
        var rows = await _service.GetChangeLogAsync(id, limit, ct);
        return Ok(rows);
    }

    private string? GetActorId() => ActorInfo.FromPrincipal(User).UserId;
}
