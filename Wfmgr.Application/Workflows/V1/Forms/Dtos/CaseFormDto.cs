namespace Wfmgr.Application.Workflows.V1.Forms.Dtos;

public sealed record CaseFormDto(
    Guid FormId,
    Guid CaseId,
    string FormType,
    int FormVersion,
    string Status,
    string PayloadJson,
    string? SubmittedBy,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
