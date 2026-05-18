using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wfmgr.Api.Auth;
using Wfmgr.Application.Workflows.V1.Config;

namespace Wfmgr.Api.Controllers;

[ApiController]
[Route("api/workflow-config")]
[Authorize(Policy = WorkflowConfigPolicies.Admin)]
public class WorkflowConfigController : ControllerBase
{
    private readonly IWorkflowConfigService _service;

    public WorkflowConfigController(IWorkflowConfigService service)
    {
        _service = service;
    }

    [HttpGet("profiles")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WorkflowProfileDto>>> GetProfiles(CancellationToken ct)
    {
        var items = await _service.GetProfilesAsync(ct);
        return Ok(items);
    }

    [HttpGet("profiles/{profileId:guid}")]
    [ProducesResponseType(typeof(WorkflowProfileDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowProfileDetailDto>> GetProfile(Guid profileId, CancellationToken ct)
    {
        var item = await _service.GetProfileAsync(profileId, ct);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost("profiles")]
    [ProducesResponseType(typeof(WorkflowProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkflowProfileDto>> CreateProfile([FromBody] CreateWorkflowProfileRequest request, CancellationToken ct)
    {
        var errors = ValidateProfile(request.Name, request.Version);
        if (errors.Count > 0)
        {
            return BadRequest(new ValidateWorkflowRuleResponse(false, errors, []));
        }

        var item = await _service.CreateProfileAsync(request, GetActorId(), ct);
        return CreatedAtAction(nameof(GetProfile), new { profileId = item.Id }, item);
    }

    [HttpPut("profiles/{profileId:guid}")]
    [ProducesResponseType(typeof(WorkflowProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowProfileDto>> UpdateProfile(Guid profileId, [FromBody] UpdateWorkflowProfileRequest request, CancellationToken ct)
    {
        var errors = ValidateProfile(request.Name, request.Version);
        if (errors.Count > 0)
        {
            return BadRequest(new ValidateWorkflowRuleResponse(false, errors, []));
        }

        var result = await _service.UpdateProfileAsync(profileId, request, GetActorId(), ct);
        if (result.NotFound) return NotFound();
        if (result.IsConflict) return Conflict(result.Conflict);
        return Ok(result.Profile);
    }

    [HttpPost("profiles/{profileId:guid}/activate")]
    [ProducesResponseType(typeof(WorkflowProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowProfileDto>> ActivateProfile(
        Guid profileId,
        [FromBody] ToggleWorkflowProfileRequest? request,
        CancellationToken ct)
    {
        request ??= new ToggleWorkflowProfileRequest(null, null);
        var result = await _service.SetProfileActiveAsync(profileId, isActive: true, request, GetActorId(), ct);
        if (result.NotFound) return NotFound();
        if (result.IsConflict) return Conflict(result.Conflict);
        return Ok(result.Profile);
    }

    [HttpPost("profiles/{profileId:guid}/deactivate")]
    [ProducesResponseType(typeof(WorkflowProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowProfileDto>> DeactivateProfile(
        Guid profileId,
        [FromBody] ToggleWorkflowProfileRequest? request,
        CancellationToken ct)
    {
        request ??= new ToggleWorkflowProfileRequest(null, null);
        var result = await _service.SetProfileActiveAsync(profileId, isActive: false, request, GetActorId(), ct);
        if (result.NotFound) return NotFound();
        if (result.IsConflict) return Conflict(result.Conflict);
        return Ok(result.Profile);
    }

    [HttpGet("profiles/{profileId:guid}/rules")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WorkflowRuleDto>>> GetRules(
        Guid profileId,
        [FromQuery] string? slotCode,
        [FromQuery] bool? enabled,
        CancellationToken ct)
    {
        var items = await _service.GetRulesAsync(profileId, slotCode, enabled, ct);
        return Ok(items);
    }

    [HttpPost("profiles/{profileId:guid}/rules")]
    [ProducesResponseType(typeof(WorkflowRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowRuleDto>> CreateRule(Guid profileId, [FromBody] CreateWorkflowRuleRequest request, CancellationToken ct)
    {
        var validation = await _service.ValidateRuleAsync(new ValidateWorkflowRuleRequest(
            request.SlotCode,
            request.ConfigJson,
            request.ConditionJson,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.Priority), ct);

        if (!validation.IsValid)
        {
            return BadRequest(validation);
        }

        var result = await _service.CreateRuleAsync(profileId, request, GetActorId(), ct);
        if (result.IsValidationError)
        {
            return BadRequest(result.ValidationError);
        }
        if (result.NotFound)
        {
            return NotFound();
        }

        var item = result.Rule!;
        return CreatedAtAction(nameof(GetRule), new { ruleId = item.Id }, item);
    }

    [HttpGet("rules/{ruleId:guid}")]
    [ProducesResponseType(typeof(WorkflowRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowRuleDto>> GetRule(Guid ruleId, CancellationToken ct)
    {
        var item = await _service.GetRuleAsync(ruleId, ct);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPut("rules/{ruleId:guid}")]
    [ProducesResponseType(typeof(WorkflowRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowRuleDto>> UpdateRule(Guid ruleId, [FromBody] UpdateWorkflowRuleRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateRuleAsync(ruleId, request, GetActorId(), ct);
        if (result.IsValidationError)
        {
            return BadRequest(result.ValidationError);
        }
        if (result.NotFound)
        {
            return NotFound();
        }
        if (result.IsConflict)
        {
            return Conflict(result.Conflict);
        }

        return Ok(result.Rule);
    }

    [HttpPost("rules/{ruleId:guid}/disable")]
    [ProducesResponseType(typeof(WorkflowRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowRuleDto>> DisableRule(
        Guid ruleId,
        [FromBody] ToggleWorkflowRuleRequest? request,
        CancellationToken ct)
    {
        request ??= new ToggleWorkflowRuleRequest(null, null);
        var result = await _service.SetRuleEnabledAsync(ruleId, enabled: false, request, GetActorId(), ct);
        if (result.NotFound) return NotFound();
        if (result.IsConflict) return Conflict(result.Conflict);
        return Ok(result.Rule);
    }

    [HttpPost("rules/{ruleId:guid}/enable")]
    [ProducesResponseType(typeof(WorkflowRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowRuleDto>> EnableRule(
        Guid ruleId,
        [FromBody] ToggleWorkflowRuleRequest? request,
        CancellationToken ct)
    {
        request ??= new ToggleWorkflowRuleRequest(null, null);
        var result = await _service.SetRuleEnabledAsync(ruleId, enabled: true, request, GetActorId(), ct);
        if (result.NotFound) return NotFound();
        if (result.IsConflict) return Conflict(result.Conflict);
        return Ok(result.Rule);
    }

    [HttpPost("rules/validate")]
    [ProducesResponseType(typeof(ValidateWorkflowRuleResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ValidateWorkflowRuleResponse>> ValidateRule([FromBody] ValidateWorkflowRuleRequest request, CancellationToken ct)
    {
        var result = await _service.ValidateRuleAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("slot-codes")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowSlotCodeDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<WorkflowSlotCodeDto>> GetSlotCodes()
    {
        return Ok(_service.GetSlotCodes());
    }

    [HttpGet("effective")]
    [ProducesResponseType(typeof(EffectiveWorkflowConfigDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EffectiveWorkflowConfigDto>> GetEffective(
        [FromQuery] string? hospitalId,
        [FromQuery] string? siteId,
        [FromQuery] string? departmentId,
        CancellationToken ct)
    {
        var result = await _service.GetEffectiveConfigAsync(hospitalId, siteId, departmentId, ct);
        return Ok(result);
    }

    [HttpGet("profiles/{profileId:guid}/changelog")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowConfigChangeLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<WorkflowConfigChangeLogDto>>> GetChangeLog(
        Guid profileId,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var existing = await _service.GetProfileAsync(profileId, ct);
        if (existing is null)
        {
            return NotFound();
        }

        var rows = await _service.GetChangeLogAsync(profileId, limit, ct);
        return Ok(rows);
    }

    private string? GetActorId()
    {
        var actor = ActorInfo.FromPrincipal(User);
        return actor.UserId;
    }

    private static List<string> ValidateProfile(string? name, int? version)
    {
        var errors = new List<string>();

        if (name is not null && string.IsNullOrWhiteSpace(name))
        {
            errors.Add("name is required.");
        }

        if (version.HasValue && version.Value < 1)
        {
            errors.Add("version must be greater than or equal to 1.");
        }

        return errors;
    }
}
