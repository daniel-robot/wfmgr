namespace Wfmgr.Application.Workflows.V1.CaseStatuses;

/// <summary>Read DTO for a single case-status overlay row.</summary>
public record CaseStatusOverlayDto(
    string Code,
    int Value,
    string? DisplayName,
    string? Description,
    string? Color,
    string? Category,
    int SortOrder,
    string ConcurrencyHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public record UpdateCaseStatusOverlayRequest(
    string? DisplayName,
    string? Description,
    string? Color,
    string? Category,
    int? SortOrder,
    string? ExpectedHash);

public record ValidateCaseStatusOverlayResponse(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public record CaseStatusOverlayMutationConflictDto(string Message, string? CurrentHash);

public class CaseStatusOverlayMutationResult
{
    public CaseStatusOverlayDto? Overlay { get; private init; }
    public ValidateCaseStatusOverlayResponse? ValidationError { get; private init; }
    public CaseStatusOverlayMutationConflictDto? Conflict { get; private init; }
    public bool NotFound { get; private init; }

    public bool IsSuccess => Overlay is not null && ValidationError is null && Conflict is null && !NotFound;
    public bool IsValidationError => ValidationError is not null;
    public bool IsConflict => Conflict is not null;

    public static CaseStatusOverlayMutationResult Success(CaseStatusOverlayDto overlay) => new() { Overlay = overlay };
    public static CaseStatusOverlayMutationResult Invalid(ValidateCaseStatusOverlayResponse response) => new() { ValidationError = response };
    public static CaseStatusOverlayMutationResult NotFoundResult() => new() { NotFound = true };
    public static CaseStatusOverlayMutationResult ConflictResult(CaseStatusOverlayMutationConflictDto conflict) => new() { Conflict = conflict };
}
