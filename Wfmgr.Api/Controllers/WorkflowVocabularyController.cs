using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wfmgr.Api.Auth;
using Wfmgr.Application.Abstractions;
using Wfmgr.Application.Workflows.V1.Config;
using Wfmgr.Application.Workflows.V1.Vocabulary;

namespace Wfmgr.Api.Controllers;

/// <summary>
/// Admin endpoints for managing the DB-backed workflow vocabulary catalog
/// (roles, work-item types, case-form types). Mirrors the conventions of
/// <see cref="WorkflowTransitionsController"/>.
/// </summary>
[ApiController]
[Route("api/workflow-vocabulary")]
[Authorize(Policy = WorkflowConfigPolicies.Admin)]
public class WorkflowVocabularyController : ControllerBase
{
    private readonly IWorkflowVocabularyCatalogService _service;
    private readonly IActorAccessor _actorAccessor;

    public WorkflowVocabularyController(
        IWorkflowVocabularyCatalogService service,
        IActorAccessor actorAccessor)
    {
        _service = service;
        _actorAccessor = actorAccessor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowVocabularyTermDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WorkflowVocabularyTermDto>>> List(
        [FromQuery] string? kind,
        CancellationToken ct)
    {
        var items = string.IsNullOrWhiteSpace(kind)
            ? await _service.ListAllAsync(ct)
            : await _service.ListByKindAsync(kind, ct);
        return Ok(items);
    }

    [HttpGet("kinds")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<string>> Kinds() => Ok(WorkflowVocabularyKinds.All);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkflowVocabularyTermDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowVocabularyTermDto>> Get(Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(WorkflowVocabularyTermDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidateWorkflowVocabularyTermResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowVocabularyTermDto>> Create(
        [FromBody] CreateWorkflowVocabularyTermRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, GetActorId(), ct);
        if (result.IsValidationError) return BadRequest(result.ValidationError);
        if (result.IsConflict) return Conflict(result.Conflict);
        return CreatedAtAction(nameof(Get), new { id = result.Term!.Id }, result.Term);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(WorkflowVocabularyTermDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidateWorkflowVocabularyTermResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(WorkflowVocabularyMutationConflictDto), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowVocabularyTermDto>> Update(
        Guid id,
        [FromBody] UpdateWorkflowVocabularyTermRequest request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, GetActorId(), ct);
        if (result.NotFound) return NotFound();
        if (result.IsValidationError) return BadRequest(result.ValidationError);
        if (result.IsConflict) return Conflict(result.Conflict);
        return Ok(result.Term);
    }

    [HttpPost("{id:guid}/enable")]
    [ProducesResponseType(typeof(WorkflowVocabularyTermDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(WorkflowVocabularyMutationConflictDto), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowVocabularyTermDto>> Enable(
        Guid id,
        [FromBody] ToggleWorkflowVocabularyTermRequest? request,
        CancellationToken ct)
    {
        request ??= new ToggleWorkflowVocabularyTermRequest(null, null);
        var result = await _service.SetEnabledAsync(id, true, request, GetActorId(), ct);
        if (result.NotFound) return NotFound();
        if (result.IsConflict) return Conflict(result.Conflict);
        return Ok(result.Term);
    }

    [HttpPost("{id:guid}/disable")]
    [ProducesResponseType(typeof(WorkflowVocabularyTermDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(WorkflowVocabularyMutationConflictDto), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowVocabularyTermDto>> Disable(
        Guid id,
        [FromBody] ToggleWorkflowVocabularyTermRequest? request,
        CancellationToken ct)
    {
        request ??= new ToggleWorkflowVocabularyTermRequest(null, null);
        var result = await _service.SetEnabledAsync(id, false, request, GetActorId(), ct);
        if (result.NotFound) return NotFound();
        if (result.IsConflict) return Conflict(result.Conflict);
        return Ok(result.Term);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(WorkflowVocabularyTermDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidateWorkflowVocabularyTermResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(WorkflowVocabularyMutationConflictDto), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowVocabularyTermDto>> Delete(
        Guid id,
        [FromQuery] string? expectedHash,
        [FromQuery] string? changeReason,
        CancellationToken ct)
    {
        var result = await _service.DeleteAsync(
            id, new ToggleWorkflowVocabularyTermRequest(expectedHash, changeReason), GetActorId(), ct);
        if (result.NotFound) return NotFound();
        if (result.IsValidationError) return BadRequest(result.ValidationError);
        if (result.IsConflict) return Conflict(result.Conflict);
        return Ok(result.Term);
    }

    [HttpPost("validate")]
    [ProducesResponseType(typeof(ValidateWorkflowVocabularyTermResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ValidateWorkflowVocabularyTermResponse>> Validate(
        [FromBody] CreateWorkflowVocabularyTermRequest request,
        CancellationToken ct)
    {
        var result = await _service.ValidateAsync(request.Kind, request.Code, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/changelog")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowVocabularyChangeLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<WorkflowVocabularyChangeLogDto>>> GetChangeLog(
        Guid id,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var existing = await _service.GetByIdAsync(id, ct);
        if (existing is null) return NotFound();
        var rows = await _service.GetChangeLogAsync(id, limit, ct);
        return Ok(rows);
    }

    private string? GetActorId() => _actorAccessor.Current.UserId;
}
