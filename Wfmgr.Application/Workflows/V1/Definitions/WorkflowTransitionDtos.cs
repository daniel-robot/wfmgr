namespace Wfmgr.Application.Workflows.V1.Definitions;

/// <summary>
/// API DTO for a workflow transition row, including its concurrency hash so admin
/// clients can perform optimistic-concurrency updates.
/// </summary>
public sealed record WorkflowTransitionDto(
    Guid Id,
    string Code,
    string Phase,
    int SortOrder,
    string ToStatus,
    string TriggerName,
    string TriggerType,
    string? ConfigSlot,
    string? Description,
    bool IsEnabled,
    IReadOnlyList<string> FromStatuses,
    IReadOnlyList<string> RequiredRoles,
    IReadOnlyList<string> GateChecks,
    IReadOnlyList<string> SuccessActions,
    IReadOnlyList<string> FailureActions,
    IReadOnlyList<string> WorkItemsToCreate,
    string ConcurrencyHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateWorkflowTransitionRequest(
    string Code,
    string Phase,
    int SortOrder,
    string ToStatus,
    string TriggerName,
    string TriggerType,
    string? ConfigSlot,
    string? Description,
    IReadOnlyList<string> FromStatuses,
    IReadOnlyList<string>? RequiredRoles,
    IReadOnlyList<string>? GateChecks,
    IReadOnlyList<string>? SuccessActions,
    IReadOnlyList<string>? FailureActions,
    IReadOnlyList<string>? WorkItemsToCreate,
    string? ChangeReason);

public sealed record UpdateWorkflowTransitionRequest(
    string Phase,
    int SortOrder,
    string ToStatus,
    string TriggerName,
    string TriggerType,
    string? ConfigSlot,
    string? Description,
    IReadOnlyList<string> FromStatuses,
    IReadOnlyList<string>? RequiredRoles,
    IReadOnlyList<string>? GateChecks,
    IReadOnlyList<string>? SuccessActions,
    IReadOnlyList<string>? FailureActions,
    IReadOnlyList<string>? WorkItemsToCreate,
    string? ExpectedHash,
    string? ChangeReason);

public sealed record ToggleWorkflowTransitionRequest(
    string? ExpectedHash,
    string? ChangeReason);

public sealed record ValidateWorkflowTransitionResponse(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public sealed record WorkflowTransitionMutationConflictDto(
    string Message,
    string? CurrentHash);

public sealed record WorkflowTransitionMutationResult(
    WorkflowTransitionDto? Transition,
    ValidateWorkflowTransitionResponse? ValidationError,
    bool NotFound,
    WorkflowTransitionMutationConflictDto? Conflict)
{
    public bool IsSuccess => Transition is not null;
    public bool IsValidationError => ValidationError is not null;
    public bool IsConflict => Conflict is not null;

    public static WorkflowTransitionMutationResult Success(WorkflowTransitionDto t) => new(t, null, false, null);
    public static WorkflowTransitionMutationResult Invalid(ValidateWorkflowTransitionResponse v) => new(null, v, false, null);
    public static WorkflowTransitionMutationResult NotFoundResult() => new(null, null, true, null);
    public static WorkflowTransitionMutationResult ConflictResult(WorkflowTransitionMutationConflictDto c) => new(null, null, false, c);
}

public sealed record WorkflowTransitionChangeLogDto(
    long ChangeLogId,
    Guid TransitionId,
    string Code,
    string Action,
    string? ActorId,
    DateTimeOffset CreatedAt,
    string? ChangeReason,
    string? SnapshotJson);

public sealed record WorkflowMetaItemDto(string Code, string? Description);

public sealed record WorkflowMetaCatalogDto(
    IReadOnlyList<WorkflowMetaItemDto> CaseStatuses,
    IReadOnlyList<WorkflowMetaItemDto> WorkItemTypes,
    IReadOnlyList<WorkflowMetaItemDto> CaseFormTypes,
    IReadOnlyList<WorkflowMetaItemDto> Roles,
    IReadOnlyList<WorkflowMetaItemDto> GateChecks,
    IReadOnlyList<WorkflowMetaItemDto> SideEffectActions,
    IReadOnlyList<WorkflowMetaItemDto> TriggerTypes,
    IReadOnlyList<WorkflowMetaItemDto> SlotCodes);
