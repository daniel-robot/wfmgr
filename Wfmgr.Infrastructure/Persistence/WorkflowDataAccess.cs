using Microsoft.EntityFrameworkCore;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence;

public class WorkflowDataAccess : IWorkflowDataAccess
{
    private readonly WfmgrDbContext _dbContext;

    public WorkflowDataAccess(WfmgrDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CaseData?> GetCaseByIdAsync(Guid caseId, CancellationToken ct)
    {
        var entity = await _dbContext.Cases.FirstOrDefaultAsync(x => x.CaseId == caseId, ct);
        return entity is null ? null : Map(entity);
    }

    public async Task<CaseData?> GetCaseByAccessionNumberAsync(string accessionNumber, CancellationToken ct)
    {
        var entity = await _dbContext.Cases.FirstOrDefaultAsync(x => x.AccessionNumber == accessionNumber, ct);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<CaseData>> GetCasesAsync(CancellationToken ct)
    {
        var items = await _dbContext.Cases
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return items
            .Select(x => new CaseData
            {
                CaseId = x.CaseId,
                HospitalId = x.HospitalId,
                SiteId = x.SiteId,
                DepartmentId = x.DepartmentId,
                PatientId = x.PatientId,
                AccessionNumber = x.AccessionNumber,
                CurrentStatus = x.CurrentStatus,
                StatusVersion = x.StatusVersion,
                CtStudyInstanceUid = x.CtStudyInstanceUid,
                CtWadoRsUrl = x.CtWadoRsUrl,
                PvMedJobId = x.PvMedJobId,
                RtStructSeriesInstanceUid = x.RtStructSeriesInstanceUid,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToList();
    }

    public async Task<IReadOnlyList<WorkItemData>> GetWorkItemsByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var items = await _dbContext.WorkItems
            .AsNoTracking()
            .Where(x => x.CaseId == caseId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return items
            .Select(x => new WorkItemData
            {
                WorkItemId = x.WorkItemId,
                CaseId = x.CaseId,
                Type = x.Type,
                Status = x.Status,
                AssignedRole = x.AssignedRole,
                AssignedUserId = x.AssignedUserId,
                DueAt = x.DueAt,
                SlaMinutes = x.SlaMinutes,
                ExternalCorrelationId = x.ExternalCorrelationId,
                PayloadJson = x.PayloadJson,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToList();
    }

    public async Task<IReadOnlyList<AuditLogData>> GetAuditLogsByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var items = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(x => x.CaseId == caseId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return items
            .Select(x => new AuditLogData
            {
                AuditId = x.AuditId,
                CaseId = x.CaseId,
                ActorType = x.ActorType,
                ActorId = x.ActorId,
                Action = x.Action,
                FromStatus = x.FromStatus,
                ToStatus = x.ToStatus,
                SnapshotJson = x.SnapshotJson,
                CreatedAt = x.CreatedAt
            })
            .ToList();
    }

    public async Task<IReadOnlyList<AuditLogData>> GetAuditLogsAsync(CancellationToken ct)
    {
        var items = await _dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return items
            .Select(x => new AuditLogData
            {
                AuditId = x.AuditId,
                CaseId = x.CaseId,
                ActorType = x.ActorType,
                ActorId = x.ActorId,
                Action = x.Action,
                FromStatus = x.FromStatus,
                ToStatus = x.ToStatus,
                SnapshotJson = x.SnapshotJson,
                CreatedAt = x.CreatedAt
            })
            .ToList();
    }

    public Task<bool> ExternalEventExistsAsync(string source, string type, string externalId, CancellationToken ct)
    {
        return _dbContext.ExternalEvents.AnyAsync(
            x => x.Source == source && x.Type == type && x.ExternalId == externalId,
            ct);
    }

    public async Task<WorkItemData?> GetOpenWorkItemAsync(Guid caseId, string type, CancellationToken ct)
    {
        var entity = await _dbContext.WorkItems
            .FirstOrDefaultAsync(x => x.CaseId == caseId && x.Type == type && x.Status != Domain.Enums.WorkItemStatus.Done, ct);

        return entity is null ? null : Map(entity);
    }

    public async Task AddCaseAsync(CaseData item, CancellationToken ct)
    {
        await _dbContext.Cases.AddAsync(new CaseEntity
        {
            CaseId = item.CaseId,
            HospitalId = item.HospitalId,
            SiteId = item.SiteId,
            DepartmentId = item.DepartmentId,
            PatientId = item.PatientId,
            AccessionNumber = item.AccessionNumber,
            CurrentStatus = item.CurrentStatus,
            StatusVersion = item.StatusVersion,
            CtStudyInstanceUid = item.CtStudyInstanceUid,
            CtWadoRsUrl = item.CtWadoRsUrl,
            PvMedJobId = item.PvMedJobId,
            RtStructSeriesInstanceUid = item.RtStructSeriesInstanceUid,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        }, ct);
    }

    public async Task AddWorkItemAsync(WorkItemData item, CancellationToken ct)
    {
        await _dbContext.WorkItems.AddAsync(new WorkItemEntity
        {
            WorkItemId = item.WorkItemId,
            CaseId = item.CaseId,
            Type = item.Type,
            Status = item.Status,
            AssignedRole = item.AssignedRole,
            AssignedUserId = item.AssignedUserId,
            DueAt = item.DueAt,
            SlaMinutes = item.SlaMinutes,
            ExternalCorrelationId = item.ExternalCorrelationId,
            PayloadJson = item.PayloadJson,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        }, ct);
    }

    public async Task AddExternalEventAsync(ExternalEventData item, CancellationToken ct)
    {
        await _dbContext.ExternalEvents.AddAsync(new ExternalEventEntity
        {
            EventId = item.EventId,
            Source = item.Source,
            Type = item.Type,
            ExternalId = item.ExternalId,
            CaseCorrelationKey = item.CaseCorrelationKey,
            CaseId = item.CaseId,
            PayloadJson = item.PayloadJson,
            ReceivedAt = item.ReceivedAt,
            ProcessedAt = item.ProcessedAt,
            ProcessStatus = item.ProcessStatus,
            Error = item.Error
        }, ct);
    }

    public async Task AddOutboxMessageAsync(OutboxMessageData item, CancellationToken ct)
    {
        await _dbContext.OutboxMessages.AddAsync(new OutboxMessageEntity
        {
            MessageId = item.MessageId,
            CaseId = item.CaseId,
            TargetSystem = item.TargetSystem,
            Action = item.Action,
            PayloadJson = item.PayloadJson,
            Status = item.Status,
            RetryCount = item.RetryCount,
            NextRetryAt = item.NextRetryAt,
            CreatedAt = item.CreatedAt,
            LastTriedAt = item.LastTriedAt
        }, ct);
    }

    public async Task AddAuditLogAsync(AuditLogData item, CancellationToken ct)
    {
        await _dbContext.AuditLogs.AddAsync(new AuditLogEntity
        {
            AuditId = item.AuditId,
            CaseId = item.CaseId,
            ActorType = item.ActorType,
            ActorId = item.ActorId,
            Action = item.Action,
            FromStatus = item.FromStatus,
            ToStatus = item.ToStatus,
            SnapshotJson = item.SnapshotJson,
            CreatedAt = item.CreatedAt
        }, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await SyncCaseUpdatesAsync(ct);
        await SyncWorkItemUpdatesAsync(ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    private readonly Dictionary<Guid, CaseData> _trackedCases = new();
    private readonly Dictionary<Guid, WorkItemData> _trackedWorkItems = new();

    private CaseData Map(CaseEntity entity)
    {
        if (_trackedCases.TryGetValue(entity.CaseId, out var existing))
        {
            return existing;
        }

        var mapped = new CaseData
        {
            CaseId = entity.CaseId,
            HospitalId = entity.HospitalId,
            SiteId = entity.SiteId,
            DepartmentId = entity.DepartmentId,
            PatientId = entity.PatientId,
            AccessionNumber = entity.AccessionNumber,
            CurrentStatus = entity.CurrentStatus,
            StatusVersion = entity.StatusVersion,
            CtStudyInstanceUid = entity.CtStudyInstanceUid,
            CtWadoRsUrl = entity.CtWadoRsUrl,
            PvMedJobId = entity.PvMedJobId,
            RtStructSeriesInstanceUid = entity.RtStructSeriesInstanceUid,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

        _trackedCases[entity.CaseId] = mapped;
        return mapped;
    }

    private WorkItemData Map(WorkItemEntity entity)
    {
        if (_trackedWorkItems.TryGetValue(entity.WorkItemId, out var existing))
        {
            return existing;
        }

        var mapped = new WorkItemData
        {
            WorkItemId = entity.WorkItemId,
            CaseId = entity.CaseId,
            Type = entity.Type,
            Status = entity.Status,
            AssignedRole = entity.AssignedRole,
            AssignedUserId = entity.AssignedUserId,
            DueAt = entity.DueAt,
            SlaMinutes = entity.SlaMinutes,
            ExternalCorrelationId = entity.ExternalCorrelationId,
            PayloadJson = entity.PayloadJson,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

        _trackedWorkItems[entity.WorkItemId] = mapped;
        return mapped;
    }

    private async Task SyncCaseUpdatesAsync(CancellationToken ct)
    {
        if (_trackedCases.Count == 0)
        {
            return;
        }

        var ids = _trackedCases.Keys.ToList();
        var entities = await _dbContext.Cases.Where(x => ids.Contains(x.CaseId)).ToListAsync(ct);

        foreach (var entity in entities)
        {
            var tracked = _trackedCases[entity.CaseId];
            entity.PatientId = tracked.PatientId;
            entity.CurrentStatus = tracked.CurrentStatus;
            entity.StatusVersion = tracked.StatusVersion;
            entity.CtStudyInstanceUid = tracked.CtStudyInstanceUid;
            entity.CtWadoRsUrl = tracked.CtWadoRsUrl;
            entity.PvMedJobId = tracked.PvMedJobId;
            entity.RtStructSeriesInstanceUid = tracked.RtStructSeriesInstanceUid;
            entity.UpdatedAt = tracked.UpdatedAt;
        }
    }

    private async Task SyncWorkItemUpdatesAsync(CancellationToken ct)
    {
        if (_trackedWorkItems.Count == 0)
        {
            return;
        }

        var ids = _trackedWorkItems.Keys.ToList();
        var entities = await _dbContext.WorkItems.Where(x => ids.Contains(x.WorkItemId)).ToListAsync(ct);

        foreach (var entity in entities)
        {
            var tracked = _trackedWorkItems[entity.WorkItemId];
            entity.Status = tracked.Status;
            entity.AssignedUserId = tracked.AssignedUserId;
            entity.DueAt = tracked.DueAt;
            entity.SlaMinutes = tracked.SlaMinutes;
            entity.ExternalCorrelationId = tracked.ExternalCorrelationId;
            entity.PayloadJson = tracked.PayloadJson;
            entity.UpdatedAt = tracked.UpdatedAt;
        }
    }
}
