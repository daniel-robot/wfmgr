using Wfmgr.Application.Abstractions.Persistence;

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
