using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wfmgr.Application.Workflows.V1.Forms;
using Wfmgr.Application.Workflows.V1.Forms.Dtos;

namespace Wfmgr.Api.Controllers;

[ApiController]
[Route("api/cases/{caseId:guid}/forms")]
[Authorize]
public class CaseFormsController : ControllerBase
{
    private readonly ICaseFormService _caseFormService;

    public CaseFormsController(ICaseFormService caseFormService)
    {
        _caseFormService = caseFormService;
    }

    [HttpPost("draft")]
    [ProducesResponseType(typeof(CaseFormDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CaseFormDto>> CreateDraft(Guid caseId, [FromBody] CreateCaseFormDraftRequest request, CancellationToken ct)
    {
        request.CaseId = caseId;
        var form = await _caseFormService.CreateDraftFormAsync(request, ct);
        return CreatedAtAction(nameof(GetFormById), new { caseId, formId = form.FormId }, form);
    }

    [HttpPut("{formId:guid}/payload")]
    [ProducesResponseType(typeof(CaseFormDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CaseFormDto>> UpdatePayload(Guid caseId, Guid formId, [FromBody] UpdateCaseFormPayloadRequest request, CancellationToken ct)
    {
        var form = await _caseFormService.UpdateFormPayloadAsync(formId, request, ct);
        if (form.CaseId != caseId)
        {
            return NotFound();
        }

        return Ok(form);
    }

    [HttpPost("{formId:guid}/submit")]
    [ProducesResponseType(typeof(CaseFormDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CaseFormDto>> Submit(Guid caseId, Guid formId, [FromBody] SubmitCaseFormRequest request, CancellationToken ct)
    {
        var form = await _caseFormService.SubmitFormAsync(formId, request, ct);
        if (form.CaseId != caseId)
        {
            return NotFound();
        }

        return Ok(form);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CaseFormDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CaseFormDto>>> GetByCase(Guid caseId, CancellationToken ct)
    {
        var forms = await _caseFormService.GetFormsByCaseAsync(caseId, ct);
        return Ok(forms);
    }

    [HttpGet("{formId:guid}")]
    [ProducesResponseType(typeof(CaseFormDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaseFormDto>> GetFormById(Guid caseId, Guid formId, CancellationToken ct)
    {
        var form = await _caseFormService.GetFormByIdAsync(formId, ct);
        if (form is null || form.CaseId != caseId)
        {
            return NotFound();
        }

        return Ok(form);
    }

    [HttpGet("latest/{formType}")]
    [ProducesResponseType(typeof(CaseFormDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaseFormDto>> GetLatestByType(Guid caseId, string formType, CancellationToken ct)
    {
        var form = await _caseFormService.GetLatestFormByCaseAndTypeAsync(caseId, formType, ct);
        if (form is null)
        {
            return NotFound();
        }

        return Ok(form);
    }
}
