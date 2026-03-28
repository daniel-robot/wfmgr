using Wfmgr.Application.Abstractions.Persistence.Models;

namespace Wfmgr.Application.Abstractions.Persistence;

public interface IWorkflowDataAccess
{
    Task<CaseData?> GetCaseByIdAsync(Guid caseId, CancellationToken ct);
    Task<CaseData?> GetCaseByAccessionNumberAsync(string accessionNumber, CancellationToken ct);
    Task<IReadOnlyList<CaseData>> GetCasesAsync(CancellationToken ct);
    Task<IReadOnlyList<WorkItemData>> GetWorkItemsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<AuditLogData>> GetAuditLogsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<AuditLogData>> GetAuditLogsAsync(CancellationToken ct);
    Task<bool> ExternalEventExistsAsync(string source, string type, string externalId, CancellationToken ct);
    Task<WorkItemData?> GetOpenWorkItemAsync(Guid caseId, string type, CancellationToken ct);
    Task AddCaseAsync(CaseData item, CancellationToken ct);
    Task AddWorkItemAsync(WorkItemData item, CancellationToken ct);
    Task AddExternalEventAsync(ExternalEventData item, CancellationToken ct);
    Task AddOutboxMessageAsync(OutboxMessageData item, CancellationToken ct);
    Task AddAuditLogAsync(AuditLogData item, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
