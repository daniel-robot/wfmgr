namespace Wfmgr.Application.Workflows.V1;

public sealed record CaseListItemDto(
    Guid CaseId,
    string HospitalId,
    string SiteId,
    string DepartmentId,
    string? PatientId,
    string AccessionNumber,
    string CurrentStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CaseDetailsDto(
    Guid CaseId,
    string HospitalId,
    string SiteId,
    string DepartmentId,
    string? PatientId,
    string AccessionNumber,
    string CurrentStatus,
    int StatusVersion,
    string? CtStudyInstanceUid,
    string? CtWadoRsUrl,
    string? PvMedJobId,
    string? RtStructSeriesInstanceUid,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WorkItemViewDto(
    Guid WorkItemId,
    Guid CaseId,
    string Type,
    string Status,
    string AssignedRole,
    string? AssignedUserId,
    DateTimeOffset? DueAt,
    int? SlaMinutes,
    string? ExternalCorrelationId,
    string? PayloadJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AuditLogViewDto(
    Guid AuditId,
    Guid CaseId,
    string ActorType,
    string? ActorId,
    string Action,
    string? FromStatus,
    string? ToStatus,
    string SnapshotJson,
    DateTimeOffset CreatedAt);
