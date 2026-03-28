namespace Wfmgr.Application.Workflows.V1;

public interface ICaseQueryService
{
    Task<IReadOnlyList<CaseListItemDto>> GetCasesAsync(CancellationToken ct);
    Task<CaseDetailsDto?> GetCaseByIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<WorkItemViewDto>> GetWorkItemsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<AuditLogViewDto>> GetAuditLogsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<AuditLogViewDto>> GetAuditLogsAsync(CancellationToken ct);
}
