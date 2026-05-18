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

/// <summary>
/// Result of a workflow rule mutation (create / update). Either contains the resulting
/// <see cref="WorkflowRuleDto"/>, a <see cref="ValidateWorkflowRuleResponse"/> with errors
/// (e.g. unsupported slot code), or signals that the target was not found.
/// </summary>
public sealed record WorkflowRuleMutationResult(
    WorkflowRuleDto? Rule,
    ValidateWorkflowRuleResponse? ValidationError,
    bool NotFound,
    WorkflowMutationConflictDto? Conflict)
{
    public bool IsSuccess => Rule is not null;
    public bool IsValidationError => ValidationError is not null;
    public bool IsConflict => Conflict is not null;

    public static WorkflowRuleMutationResult Success(WorkflowRuleDto rule) => new(rule, null, false, null);
    public static WorkflowRuleMutationResult Invalid(ValidateWorkflowRuleResponse response) => new(null, response, false, null);
    public static WorkflowRuleMutationResult NotFoundResult() => new(null, null, true, null);
    public static WorkflowRuleMutationResult ConflictResult(WorkflowMutationConflictDto conflict) => new(null, null, false, conflict);
}

/// <summary>Result of a workflow profile mutation. Mirrors <see cref="WorkflowRuleMutationResult"/>.</summary>
public sealed record WorkflowProfileMutationResult(
    WorkflowProfileDto? Profile,
    bool NotFound,
    WorkflowMutationConflictDto? Conflict,
    IReadOnlyList<string>? Errors)
{
    public bool IsSuccess => Profile is not null;
    public bool IsConflict => Conflict is not null;
    public bool IsValidationError => Errors is { Count: > 0 };

    public static WorkflowProfileMutationResult Success(WorkflowProfileDto p) => new(p, false, null, null);
    public static WorkflowProfileMutationResult NotFoundResult() => new(null, true, null, null);
    public static WorkflowProfileMutationResult ConflictResult(WorkflowMutationConflictDto c) => new(null, false, c, null);
    public static WorkflowProfileMutationResult Invalid(IReadOnlyList<string> errors) => new(null, false, null, errors);
}

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

/// <summary>
/// Single audit row from the <c>WorkflowConfigChangeLog</c> table.
/// </summary>
public sealed record WorkflowConfigChangeLogDto(
    long ChangeLogId,
    string EntityType,
    Guid EntityId,
    Guid ProfileId,
    string Action,
    string? ActorId,
    DateTimeOffset CreatedAt,
    string? ChangeReason,
    string? SnapshotJson);
