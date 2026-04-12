using Wfmgr.Application.Abstractions.Persistence.Models;

namespace Wfmgr.Application.Abstractions.Persistence;

public interface IWorkflowDataAccess
{
    Task<CaseData?> GetCaseByIdAsync(Guid caseId, CancellationToken ct);
    Task<CaseData?> GetCaseByAccessionNumberAsync(string accessionNumber, CancellationToken ct);
    Task<CaseFormData?> GetCaseFormByIdAsync(Guid formId, CancellationToken ct);
    Task<IReadOnlyList<CaseFormData>> GetCaseFormsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<CaseFormData?> GetLatestCaseFormByCaseAndTypeAsync(Guid caseId, string formType, CancellationToken ct);
    Task<IReadOnlyList<CaseData>> GetCasesAsync(CancellationToken ct);
    Task<IReadOnlyList<CaseData>> GetCasesByPatientIdAsync(string patientId, CancellationToken ct);
    Task<IReadOnlyList<WorkItemData>> GetWorkItemsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<AuditLogData>> GetAuditLogsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<AuditLogData>> GetAuditLogsAsync(CancellationToken ct);
    Task<IReadOnlyList<CaseTransitionHistoryData>> GetCaseTransitionHistoryByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<CaseAttachmentData>> GetCaseAttachmentsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<ExternalEventData>> GetExternalEventsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<IntegrationReferenceData>> GetIntegrationReferencesByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<PlanVersionData>> GetPlanVersionsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<bool> ExternalEventExistsAsync(string source, string type, string externalId, CancellationToken ct);
    Task<WorkItemData?> GetWorkItemByIdAsync(Guid workItemId, CancellationToken ct);
    Task<WorkItemData?> GetOpenWorkItemAsync(Guid caseId, string type, CancellationToken ct);
    Task<IReadOnlyList<WorkItemData>> GetMutableWorkItemsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<bool> WorkItemExistsAsync(Guid caseId, string type, string? requiredResultCode, CancellationToken ct);
    Task<bool> CaseFormExistsAsync(Guid caseId, string formType, string? requiredStatus, CancellationToken ct);
    Task<bool> PlanVersionExistsAsync(Guid caseId, CancellationToken ct);
    Task AddCaseAsync(CaseData item, CancellationToken ct);
    Task UpdateCaseAsync(CaseData item, CancellationToken ct);
    Task AddCaseFormAsync(CaseFormData item, CancellationToken ct);
    Task UpdateCaseFormAsync(CaseFormData item, CancellationToken ct);
    Task AddWorkItemAsync(WorkItemData item, CancellationToken ct);
    Task AddExternalEventAsync(ExternalEventData item, CancellationToken ct);
    Task AddOutboxMessageAsync(OutboxMessageData item, CancellationToken ct);
    Task EnqueueOutboxAsync(Guid? caseId, string targetSystem, string action, string payloadJson, CancellationToken ct);
    Task AddAuditLogAsync(AuditLogData item, CancellationToken ct);
    Task AddCaseTransitionHistoryAsync(CaseTransitionHistoryData item, CancellationToken ct);
    Task UpsertIntegrationReferenceAsync(
        Guid caseId,
        string systemName,
        string externalEntityType,
        string externalId,
        string? externalStatus,
        string? metadataJson,
        CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
