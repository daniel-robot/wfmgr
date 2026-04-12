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

public sealed record TransitionHistoryViewDto(
    Guid TransitionId,
    Guid CaseId,
    string? FromStatus,
    string ToStatus,
    string TriggerType,
    string TriggerName,
    string? TriggeredBy,
    string? Reason,
    string? MetadataJson,
    DateTimeOffset CreatedAt);

public sealed record CaseFormViewDto(
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

public sealed record CaseAttachmentViewDto(
    Guid AttachmentId,
    Guid CaseId,
    string Category,
    string FileName,
    string StoragePath,
    string? SourceSystem,
    string? UploadedBy,
    DateTimeOffset UploadedAt);

public sealed record ExternalEventViewDto(
    Guid EventId,
    Guid? CaseId,
    string Source,
    string Type,
    string ExternalId,
    string? CaseCorrelationKey,
    string ProcessStatus,
    string? Error,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt,
    string PayloadJson);

public sealed record IntegrationReferenceViewDto(
    Guid Id,
    Guid CaseId,
    string SystemName,
    string ExternalEntityType,
    string ExternalId,
    string? ExternalStatus,
    string? MetadataJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PlanVersionViewDto(
    Guid PlanVersionId,
    Guid CaseId,
    int VersionNo,
    string SourceSystem,
    string Status,
    string? SummaryJson,
    DateTimeOffset CreatedAt);

public sealed record WorkflowOptionDto(string Value, string Label);
