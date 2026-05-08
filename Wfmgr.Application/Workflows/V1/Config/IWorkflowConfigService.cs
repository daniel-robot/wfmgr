namespace Wfmgr.Application.Workflows.V1.Config;

public interface IWorkflowConfigService
{
    Task<IReadOnlyList<WorkflowProfileDto>> GetProfilesAsync(CancellationToken ct);
    Task<WorkflowProfileDetailDto?> GetProfileAsync(Guid profileId, CancellationToken ct);
    Task<WorkflowProfileDto> CreateProfileAsync(CreateWorkflowProfileRequest request, string? actorId, CancellationToken ct);
    Task<WorkflowProfileDto?> UpdateProfileAsync(Guid profileId, UpdateWorkflowProfileRequest request, string? actorId, CancellationToken ct);
    Task<WorkflowProfileDto?> SetProfileActiveAsync(Guid profileId, bool isActive, ToggleWorkflowProfileRequest request, string? actorId, CancellationToken ct);

    Task<IReadOnlyList<WorkflowRuleDto>> GetRulesAsync(Guid profileId, string? slotCode, bool? enabled, CancellationToken ct);
    Task<WorkflowRuleDto?> GetRuleAsync(Guid ruleId, CancellationToken ct);
    Task<WorkflowRuleDto?> CreateRuleAsync(Guid profileId, CreateWorkflowRuleRequest request, string? actorId, CancellationToken ct);
    Task<WorkflowRuleDto?> UpdateRuleAsync(Guid ruleId, UpdateWorkflowRuleRequest request, string? actorId, CancellationToken ct);
    Task<WorkflowRuleDto?> SetRuleEnabledAsync(Guid ruleId, bool enabled, ToggleWorkflowRuleRequest request, string? actorId, CancellationToken ct);

    Task<ValidateWorkflowRuleResponse> ValidateRuleAsync(ValidateWorkflowRuleRequest request, CancellationToken ct);
    IReadOnlyList<WorkflowSlotCodeDto> GetSlotCodes();
    Task<EffectiveWorkflowConfigDto> GetEffectiveConfigAsync(string? hospitalId, string? siteId, string? departmentId, CancellationToken ct);
}
