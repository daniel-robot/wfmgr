namespace Wfmgr.Application.Workflows.V1.Config;

public sealed record WorkflowProfileDto(
    Guid Id,
    string Key,
    string? Name,
    int Version,
    string? HospitalId,
    string? SiteId,
    string? DepartmentId,
    bool IsActive,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record WorkflowRuleDto(
    Guid Id,
    Guid ProfileId,
    string SlotCode,
    int Priority,
    bool Enabled,
    string? ConditionJson,
    string ConfigJson,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record WorkflowProfileDetailDto(
    WorkflowProfileDto Profile,
    IReadOnlyList<WorkflowRuleDto> Rules);

public sealed record CreateWorkflowProfileRequest(
    string Name,
    int Version,
    string? HospitalId,
    string? SiteId,
    string? DepartmentId,
    bool IsActive);

public sealed record UpdateWorkflowProfileRequest(
    string? Name,
    int? Version,
    string? HospitalId,
    string? SiteId,
    string? DepartmentId,
    bool? IsActive);

public sealed record CreateWorkflowRuleRequest(
    string SlotCode,
    int Priority,
    bool Enabled,
    string? ConditionJson,
    string ConfigJson,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public sealed record UpdateWorkflowRuleRequest(
    string SlotCode,
    int Priority,
    bool Enabled,
    string? ConditionJson,
    string ConfigJson,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public sealed record ValidateWorkflowRuleRequest(
    string SlotCode,
    string ConfigJson,
    string? ConditionJson,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    int? Priority);

public sealed record ValidateWorkflowRuleResponse(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public sealed record WorkflowSlotCodeDto(
    string Code,
    string Name,
    string? Description);

public sealed record EffectiveWorkflowSlotDto(
    string SlotCode,
    Guid? SourceProfileId,
    Guid? RuleId,
    int? Priority,
    string? ConfigJson);

public sealed record EffectiveWorkflowConfigDto(
    Guid? MatchedProfileId,
    string? MatchedProfileKey,
    int? MatchedProfileVersion,
    IReadOnlyList<EffectiveWorkflowSlotDto> ResolvedSlots);
