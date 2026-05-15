namespace Wfmgr.Application.Workflows.V1.Vocabulary;

/// <summary>Read DTO for a single vocabulary term.</summary>
public record WorkflowVocabularyTermDto(
    Guid Id,
    string Kind,
    string Code,
    string? DisplayName,
    string? Description,
    int SortOrder,
    bool IsSystem,
    bool IsEnabled,
    string ConcurrencyHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public record CreateWorkflowVocabularyTermRequest(
    string Kind,
    string Code,
    string? DisplayName,
    string? Description,
    int? SortOrder,
    string? ChangeReason);

public record UpdateWorkflowVocabularyTermRequest(
    string? DisplayName,
    string? Description,
    int? SortOrder,
    string? ExpectedHash,
    string? ChangeReason);

public record ToggleWorkflowVocabularyTermRequest(
    string? ExpectedHash,
    string? ChangeReason);

public record ValidateWorkflowVocabularyTermResponse(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public record WorkflowVocabularyMutationConflictDto(
    string Message,
    string? CurrentHash);

public record WorkflowVocabularyChangeLogDto(
    long ChangeLogId,
    Guid TermId,
    string Kind,
    string Code,
    string Action,
    string? ActorId,
    DateTimeOffset CreatedAt,
    string? ChangeReason,
    string? SnapshotJson);

/// <summary>Result discriminator for vocabulary mutations.</summary>
public class WorkflowVocabularyMutationResult
{
    public WorkflowVocabularyTermDto? Term { get; private init; }
    public ValidateWorkflowVocabularyTermResponse? ValidationError { get; private init; }
    public WorkflowVocabularyMutationConflictDto? Conflict { get; private init; }
    public bool NotFound { get; private init; }

    public bool IsSuccess => Term is not null && ValidationError is null && Conflict is null && !NotFound;
    public bool IsValidationError => ValidationError is not null;
    public bool IsConflict => Conflict is not null;

    public static WorkflowVocabularyMutationResult Success(WorkflowVocabularyTermDto term) =>
        new() { Term = term };

    public static WorkflowVocabularyMutationResult Invalid(ValidateWorkflowVocabularyTermResponse response) =>
        new() { ValidationError = response };

    public static WorkflowVocabularyMutationResult NotFoundResult() => new() { NotFound = true };

    public static WorkflowVocabularyMutationResult ConflictResult(WorkflowVocabularyMutationConflictDto conflict) =>
        new() { Conflict = conflict };
}
