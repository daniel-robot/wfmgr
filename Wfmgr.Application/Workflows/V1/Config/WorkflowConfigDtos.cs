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
    string ConcurrencyHash,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record WorkflowRuleDto(
    Guid Id,
    Guid ProfileId,
    string SlotCode,
    int Priority,
    bool Enabled,
    string ConcurrencyHash,
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
    bool IsActive,
    string? ChangeReason);

public sealed record UpdateWorkflowProfileRequest(
    string? Name,
    int? Version,
    string? HospitalId,
    string? SiteId,
    string? DepartmentId,
    bool? IsActive,
    string? ExpectedHash,
    string? ChangeReason);

public sealed record ToggleWorkflowProfileRequest(
    string? ExpectedHash,
    string? ChangeReason);

public sealed record CreateWorkflowRuleRequest(
    string SlotCode,
    int Priority,
    bool Enabled,
    string? ConditionJson,
    string ConfigJson,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? ChangeReason);

public sealed record UpdateWorkflowRuleRequest(
    string SlotCode,
    int Priority,
    bool Enabled,
    string? ConditionJson,
    string ConfigJson,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? ExpectedHash,
    string? ChangeReason);

public sealed record ToggleWorkflowRuleRequest(
    string? ExpectedHash,
    string? ChangeReason);

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

public sealed record WorkflowMutationConflictDto(
    string Message,
    string? CurrentHash);

public sealed record EffectiveWorkflowQueryDto(
    string? HospitalId,
    string? SiteId,
    string? DepartmentId);

public sealed record EffectiveWorkflowMatchedProfileDto(
    Guid Id,
    string Key,
    int Version,
    string? HospitalId,
    string? SiteId,
    string? DepartmentId);

public sealed record EffectiveWorkflowSlotDto(
    string SlotCode,
    Guid? SourceProfileId,
    string? SourceProfileKey,
    Guid? RuleId,
    int? Priority,
    bool? Enabled,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? ConfigJson,
    string ResolutionReason);

public sealed record EffectiveWorkflowUnmatchedSlotDto(
    string SlotCode,
    string Reason);

public sealed record EffectiveWorkflowEvaluatedProfileDto(
    Guid ProfileId,
    string Key,
    int Version,
    string? HospitalId,
    string? SiteId,
    string? DepartmentId,
    bool IsActive,
    bool MatchedScope,
    string ReasonIncludedOrSkipped);

public sealed record EffectiveWorkflowConfigDto(
    EffectiveWorkflowQueryDto Query,
    EffectiveWorkflowMatchedProfileDto? MatchedProfile,
    IReadOnlyList<EffectiveWorkflowSlotDto> ResolvedSlots,
    IReadOnlyList<EffectiveWorkflowUnmatchedSlotDto> UnmatchedSlots,
    IReadOnlyList<EffectiveWorkflowEvaluatedProfileDto> EvaluatedProfiles);
