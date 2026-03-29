namespace Wfmgr.Application.Workflows.V1;

public interface ICaseQueryService
{
    Task<IReadOnlyList<CaseListItemDto>> GetCasesAsync(CancellationToken ct);
    Task<CaseDetailsDto?> GetCaseByIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<WorkItemViewDto>> GetWorkItemsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<AuditLogViewDto>> GetAuditLogsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<TransitionHistoryViewDto>> GetTransitionHistoryByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<CaseFormViewDto>> GetFormsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<CaseAttachmentViewDto>> GetAttachmentsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<ExternalEventViewDto>> GetExternalEventsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<IntegrationReferenceViewDto>> GetIntegrationReferencesByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<PlanVersionViewDto>> GetPlanVersionsByCaseIdAsync(Guid caseId, CancellationToken ct);
    IReadOnlyList<WorkflowOptionDto> GetWorkflowStatuses();
    IReadOnlyList<WorkflowOptionDto> GetWorkflowWorkItemTypes();
    Task<IReadOnlyList<AuditLogViewDto>> GetAuditLogsAsync(CancellationToken ct);
}
