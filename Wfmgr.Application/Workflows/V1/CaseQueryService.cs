using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Domain.Enums;
using Wfmgr.Domain.WorkItems;

namespace Wfmgr.Application.Workflows.V1;

public class CaseQueryService : ICaseQueryService
{
    private readonly IWorkflowDataAccess _dataAccess;

    public CaseQueryService(IWorkflowDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    public async Task<IReadOnlyList<CaseListItemDto>> GetCasesAsync(CancellationToken ct)
    {
        var items = await _dataAccess.GetCasesAsync(ct);

        return items
            .Select(x => new CaseListItemDto(
                x.CaseId,
                x.HospitalId,
                x.SiteId,
                x.DepartmentId,
                x.PatientId,
                x.AccessionNumber,
                x.CurrentStatus.ToString(),
                x.CreatedAt,
                x.UpdatedAt))
            .ToList();
    }

    public async Task<CaseDetailsDto?> GetCaseByIdAsync(Guid caseId, CancellationToken ct)
    {
        var x = await _dataAccess.GetCaseByIdAsync(caseId, ct);
        if (x is null)
        {
            return null;
        }

        return new CaseDetailsDto(
            x.CaseId,
            x.HospitalId,
            x.SiteId,
            x.DepartmentId,
            x.PatientId,
            x.AccessionNumber,
            x.CurrentStatus.ToString(),
            x.StatusVersion,
            x.CtStudyInstanceUid,
            x.CtWadoRsUrl,
            x.PvMedJobId,
            x.RtStructSeriesInstanceUid,
            x.CreatedAt,
            x.UpdatedAt);
    }

    public async Task<IReadOnlyList<WorkItemViewDto>> GetWorkItemsByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var items = await _dataAccess.GetWorkItemsByCaseIdAsync(caseId, ct);

        return items
            .Select(x => new WorkItemViewDto(
                x.WorkItemId,
                x.CaseId,
                x.Type,
                x.Status.ToString(),
                x.AssignedRole,
                x.AssignedUserId,
                x.DueAt,
                x.SlaMinutes,
                x.ExternalCorrelationId,
                x.PayloadJson,
                x.CreatedAt,
                x.UpdatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<AuditLogViewDto>> GetAuditLogsByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var items = await _dataAccess.GetAuditLogsByCaseIdAsync(caseId, ct);

        return items
            .Select(x => new AuditLogViewDto(
                x.AuditId,
                x.CaseId,
                x.ActorType,
                x.ActorId,
                x.Action,
                x.FromStatus?.ToString(),
                x.ToStatus?.ToString(),
                x.SnapshotJson,
                x.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<TransitionHistoryViewDto>> GetTransitionHistoryByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var items = await _dataAccess.GetCaseTransitionHistoryByCaseIdAsync(caseId, ct);

        return items
            .Select(x => new TransitionHistoryViewDto(
                x.TransitionId,
                x.CaseId,
                x.FromStatus,
                x.ToStatus,
                x.TriggerType,
                x.TriggerName,
                x.TriggeredBy,
                x.Reason,
                x.MetadataJson,
                x.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<CaseFormViewDto>> GetFormsByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var items = await _dataAccess.GetCaseFormsByCaseIdAsync(caseId, ct);

        return items
            .Select(x => new CaseFormViewDto(
                x.FormId,
                x.CaseId,
                x.FormType,
                x.FormVersion,
                x.Status,
                x.PayloadJson,
                x.SubmittedBy,
                x.SubmittedAt,
                x.CreatedAt,
                x.UpdatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<CaseAttachmentViewDto>> GetAttachmentsByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var items = await _dataAccess.GetCaseAttachmentsByCaseIdAsync(caseId, ct);

        return items
            .Select(x => new CaseAttachmentViewDto(
                x.AttachmentId,
                x.CaseId,
                x.Category,
                x.FileName,
                x.StoragePath,
                x.SourceSystem,
                x.UploadedBy,
                x.UploadedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<ExternalEventViewDto>> GetExternalEventsByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var items = await _dataAccess.GetExternalEventsByCaseIdAsync(caseId, ct);

        return items
            .Select(x => new ExternalEventViewDto(
                x.EventId,
                x.CaseId,
                x.Source,
                x.Type,
                x.ExternalId,
                x.CaseCorrelationKey,
                x.ProcessStatus,
                x.Error,
                x.ReceivedAt,
                x.ProcessedAt,
                x.PayloadJson))
            .ToList();
    }

    public async Task<IReadOnlyList<IntegrationReferenceViewDto>> GetIntegrationReferencesByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var items = await _dataAccess.GetIntegrationReferencesByCaseIdAsync(caseId, ct);

        return items
            .Select(x => new IntegrationReferenceViewDto(
                x.Id,
                x.CaseId,
                x.SystemName,
                x.ExternalEntityType,
                x.ExternalId,
                x.ExternalStatus,
                x.MetadataJson,
                x.CreatedAt,
                x.UpdatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<PlanVersionViewDto>> GetPlanVersionsByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var items = await _dataAccess.GetPlanVersionsByCaseIdAsync(caseId, ct);

        return items
            .Select(x => new PlanVersionViewDto(
                x.PlanVersionId,
                x.CaseId,
                x.VersionNo,
                x.SourceSystem,
                x.Status,
                x.SummaryJson,
                x.CreatedAt))
            .ToList();
    }

    public IReadOnlyList<WorkflowOptionDto> GetWorkflowStatuses()
    {
        return Enum.GetValues<CaseStatus>()
            .Select(x => new WorkflowOptionDto(x.ToString(), x.ToString()))
            .ToList();
    }

    public IReadOnlyList<WorkflowOptionDto> GetWorkflowWorkItemTypes()
    {
        return typeof(WorkItemTypes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(x => x.IsLiteral && x.FieldType == typeof(string))
            .Select(x => x.GetRawConstantValue()?.ToString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => new WorkflowOptionDto(x!, x!))
            .OrderBy(x => x.Value)
            .ToList();
    }

    public async Task<IReadOnlyList<AuditLogViewDto>> GetAuditLogsAsync(CancellationToken ct)
    {
        var items = await _dataAccess.GetAuditLogsAsync(ct);

        return items
            .Select(x => new AuditLogViewDto(
                x.AuditId,
                x.CaseId,
                x.ActorType,
                x.ActorId,
                x.Action,
                x.FromStatus?.ToString(),
                x.ToStatus?.ToString(),
                x.SnapshotJson,
                x.CreatedAt))
            .ToList();
    }
}
