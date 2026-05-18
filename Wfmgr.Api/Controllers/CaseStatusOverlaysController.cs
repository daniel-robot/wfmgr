using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wfmgr.Application.Workflows.V1.CaseStatuses;
using Wfmgr.Application.Workflows.V1.Config;

namespace Wfmgr.Api.Controllers;

/// <summary>
/// Admin endpoints for the cosmetic <c>CaseStatus</c> overlay.
/// Read-only enum codes; admins can update display metadata only.
/// </summary>
[ApiController]
[Route("api/case-status-overlays")]
[Authorize(Policy = WorkflowConfigPolicies.Admin)]
public class CaseStatusOverlaysController : ControllerBase
{
    private readonly ICaseStatusOverlayService _service;

    public CaseStatusOverlaysController(ICaseStatusOverlayService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CaseStatusOverlayDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CaseStatusOverlayDto>>> List(CancellationToken ct)
    {
        var items = await _service.ListAllAsync(ct);
        return Ok(items);
    }

    [HttpGet("{code}")]
    [ProducesResponseType(typeof(CaseStatusOverlayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaseStatusOverlayDto>> Get(string code, CancellationToken ct)
    {
        var item = await _service.GetByCodeAsync(code, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("{code}")]
    [ProducesResponseType(typeof(CaseStatusOverlayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidateCaseStatusOverlayResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(CaseStatusOverlayMutationConflictDto), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CaseStatusOverlayDto>> Update(
        string code,
        [FromBody] UpdateCaseStatusOverlayRequest request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(code, request, ct);
        if (result.NotFound) return NotFound();
        if (result.IsValidationError) return BadRequest(result.ValidationError);
        if (result.IsConflict) return Conflict(result.Conflict);
        return Ok(result.Overlay);
    }

    [HttpPost("{code}/reset")]
    [ProducesResponseType(typeof(CaseStatusOverlayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(CaseStatusOverlayMutationConflictDto), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CaseStatusOverlayDto>> Reset(string code, CancellationToken ct)
    {
        var result = await _service.ResetAsync(code, ct);
        if (result.NotFound) return NotFound();
        if (result.IsValidationError) return BadRequest(result.ValidationError);
        if (result.IsConflict) return Conflict(result.Conflict);
        return Ok(result.Overlay);
    }
}
