using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wfmgr.Application.Patients;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Dtos;

namespace Wfmgr.Api.Controllers;

[ApiController]
[Route("api/patients")]
[Authorize]
public class PatientsController : ControllerBase
{
    private readonly IPatientService _patientService;
    private readonly ICaseWorkflowService _workflowService;
    private readonly ICaseQueryService _queryService;

    public PatientsController(
        IPatientService patientService,
        ICaseWorkflowService workflowService,
        ICaseQueryService queryService)
    {
        _patientService = patientService;
        _workflowService = workflowService;
        _queryService = queryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PatientDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PatientDto>>> GetPatients(CancellationToken ct)
    {
        var patients = await _patientService.GetPatientsAsync(ct);
        return Ok(patients);
    }

    [HttpGet("{patientId:guid}")]
    [ProducesResponseType(typeof(PatientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientDto>> GetPatient(Guid patientId, CancellationToken ct)
    {
        var patient = await _patientService.GetPatientByIdAsync(patientId, ct);
        if (patient is null)
        {
            return NotFound();
        }

        return Ok(patient);
    }

    [HttpPost]
    [ProducesResponseType(typeof(PatientDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<PatientDto>> CreatePatient(
        [FromBody] CreatePatientRequest request,
        CancellationToken ct)
    {
        var patient = await _patientService.CreatePatientAsync(request, ct);
        return CreatedAtAction(nameof(GetPatient), new { patientId = patient.PatientId }, patient);
    }

    [HttpGet("{patientId:guid}/cases")]
    [ProducesResponseType(typeof(IReadOnlyList<CaseListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CaseListItemDto>>> GetPatientCases(Guid patientId, CancellationToken ct)
    {
        var cases = await _queryService.GetCasesByPatientIdAsync(patientId.ToString(), ct);
        return Ok(cases);
    }

    [HttpPost("{patientId:guid}/cases")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> StartWorkflow(
        Guid patientId,
        [FromBody] StartWorkflowRequest request,
        CancellationToken ct)
    {
        var patient = await _patientService.GetPatientByIdAsync(patientId, ct);
        if (patient is null)
        {
            return NotFound();
        }

        var createRequest = new CreateCaseRequest
        {
            HospitalId = patient.HospitalId,
            SiteId = patient.SiteId,
            DepartmentId = patient.DepartmentId,
            AccessionNumber = request.AccessionNumber,
            PatientId = patient.PatientId.ToString(),
            Notes = request.Notes
        };

        var caseId = await _workflowService.CreateCaseAsync(createRequest, ct);
        return CreatedAtAction(
            nameof(CasesController.GetCase),
            "Cases",
            new { caseId },
            new { caseId });
    }
}

public class StartWorkflowRequest
{
    public string AccessionNumber { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
