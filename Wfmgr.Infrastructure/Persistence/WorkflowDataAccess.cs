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

    public async Task<CaseFormData?> GetCaseFormByIdAsync(Guid formId, CancellationToken ct)
    {
        var entity = await _dbContext.CaseForms.AsNoTracking().FirstOrDefaultAsync(x => x.FormId == formId, ct);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<CaseFormData>> GetCaseFormsByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var entities = await _dbContext.CaseForms
            .AsNoTracking()
            .Where(x => x.CaseId == caseId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.FormVersion)
            .ToListAsync(ct);

        return entities.Select(Map).ToList();
    }

    public async Task<CaseFormData?> GetLatestCaseFormByCaseAndTypeAsync(Guid caseId, string formType, CancellationToken ct)
    {
        var entity = await _dbContext.CaseForms
            .AsNoTracking()
            .Where(x => x.CaseId == caseId && x.FormType == formType)
            .OrderByDescending(x => x.FormVersion)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<CaseData>> GetCasesByPatientIdAsync(string patientId, CancellationToken ct)
    {
        var items = await _dbContext.Cases
            .AsNoTracking()
            .Where(x => x.PatientId == patientId)
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
                Notes = x.Notes,
                CurrentPlannerUserId = x.CurrentPlannerUserId,
                CurrentReviewerUserId = x.CurrentReviewerUserId,
                CurrentPlanVersionNo = x.CurrentPlanVersionNo,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToList();
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
                Notes = x.Notes,
                CurrentPlannerUserId = x.CurrentPlannerUserId,
                CurrentReviewerUserId = x.CurrentReviewerUserId,
                CurrentPlanVersionNo = x.CurrentPlanVersionNo,
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
                SequenceNo = x.SequenceNo,
                ParentWorkItemId = x.ParentWorkItemId,
                Type = x.Type,
                Status = x.Status,
                WorkItemGroup = x.WorkItemGroup,
                AssignedRole = x.AssignedRole,
                AssignedUserId = x.AssignedUserId,
                DueAt = x.DueAt,
                SlaMinutes = x.SlaMinutes,
                ExternalCorrelationId = x.ExternalCorrelationId,
                ResultCode = x.ResultCode,
                CompletedAt = x.CompletedAt,
                CompletedBy = x.CompletedBy,
                FormId = x.FormId,
                RequiresDifferentUserFrom = x.RequiresDifferentUserFrom,
                RetryCount = x.RetryCount,
                Remarks = x.Remarks,
                PayloadJson = x.PayloadJson,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToList();
    }

    public async Task<WorkItemData?> GetWorkItemByIdAsync(Guid workItemId, CancellationToken ct)
    {
        var entity = await _dbContext.WorkItems.FirstOrDefaultAsync(x => x.WorkItemId == workItemId, ct);
        return entity is null ? null : Map(entity);
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

    public async Task<IReadOnlyList<CaseTransitionHistoryData>> GetCaseTransitionHistoryByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var items = await _dbContext.CaseTransitionHistories
            .AsNoTracking()
            .Where(x => x.CaseId == caseId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return items.Select(x => new CaseTransitionHistoryData
        {
            TransitionId = x.TransitionId,
            CaseId = x.CaseId,
            FromStatus = x.FromStatus,
            ToStatus = x.ToStatus,
            TriggerType = x.TriggerType,
            TriggerName = x.TriggerName,
            TriggeredBy = x.TriggeredBy,
            Reason = x.Reason,
            MetadataJson = x.MetadataJson,
            CreatedAt = x.CreatedAt
        }).ToList();
    }

    public async Task<IReadOnlyList<CaseAttachmentData>> GetCaseAttachmentsByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var items = await _dbContext.CaseAttachments
            .AsNoTracking()
            .Where(x => x.CaseId == caseId)
            .OrderByDescending(x => x.UploadedAt)
            .ToListAsync(ct);

        return items.Select(x => new CaseAttachmentData
        {
            AttachmentId = x.AttachmentId,
            CaseId = x.CaseId,
            Category = x.Category,
            FileName = x.FileName,
            StoragePath = x.StoragePath,
            SourceSystem = x.SourceSystem,
            UploadedBy = x.UploadedBy,
            UploadedAt = x.UploadedAt
        }).ToList();
    }

    public async Task<IReadOnlyList<ExternalEventData>> GetExternalEventsByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var items = await _dbContext.ExternalEvents
            .AsNoTracking()
            .Where(x => x.CaseId == caseId)
            .OrderByDescending(x => x.ReceivedAt)
            .ToListAsync(ct);

        return items.Select(x => new ExternalEventData
        {
            EventId = x.EventId,
            Source = x.Source,
            Type = x.Type,
            ExternalId = x.ExternalId,
            CaseCorrelationKey = x.CaseCorrelationKey,
            CaseId = x.CaseId,
            PayloadJson = x.PayloadJson,
            ReceivedAt = x.ReceivedAt,
            ProcessedAt = x.ProcessedAt,
            ProcessStatus = x.ProcessStatus,
            Error = x.Error
        }).ToList();
    }

    public async Task<IReadOnlyList<IntegrationReferenceData>> GetIntegrationReferencesByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var items = await _dbContext.IntegrationReferences
            .AsNoTracking()
            .Where(x => x.CaseId == caseId)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(ct);

        return items.Select(x => new IntegrationReferenceData
        {
            Id = x.Id,
            CaseId = x.CaseId,
            SystemName = x.SystemName,
            ExternalEntityType = x.ExternalEntityType,
            ExternalId = x.ExternalId,
            ExternalStatus = x.ExternalStatus,
            MetadataJson = x.MetadataJson,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        }).ToList();
    }

    public async Task<IReadOnlyList<PlanVersionData>> GetPlanVersionsByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var items = await _dbContext.PlanVersions
            .AsNoTracking()
            .Where(x => x.CaseId == caseId)
            .OrderByDescending(x => x.VersionNo)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return items.Select(x => new PlanVersionData
        {
            PlanVersionId = x.PlanVersionId,
            CaseId = x.CaseId,
            VersionNo = x.VersionNo,
            SourceSystem = x.SourceSystem,
            Status = x.Status,
            SummaryJson = x.SummaryJson,
            CreatedAt = x.CreatedAt
        }).ToList();
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
            .FirstOrDefaultAsync(x =>
                x.CaseId == caseId
                && x.Type == type
                && x.Status != Domain.Enums.WorkItemStatus.Done
                && x.Status != Domain.Enums.WorkItemStatus.Rejected
                && x.Status != Domain.Enums.WorkItemStatus.Cancelled
                && x.Status != Domain.Enums.WorkItemStatus.Skipped,
                ct);

        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<WorkItemData>> GetMutableWorkItemsByCaseIdAsync(Guid caseId, CancellationToken ct)
    {
        var entities = await _dbContext.WorkItems
            .Where(x => x.CaseId == caseId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(Map).ToList();
    }

    public Task<bool> WorkItemExistsAsync(Guid caseId, string type, string? requiredResultCode, CancellationToken ct)
    {
        var query = _dbContext.WorkItems.AsQueryable()
            .Where(x => x.CaseId == caseId && x.Type == type);

        if (!string.IsNullOrWhiteSpace(requiredResultCode))
        {
            query = query.Where(x => x.ResultCode == requiredResultCode);
        }

        return query.AnyAsync(ct);
    }

    public Task<bool> CaseFormExistsAsync(Guid caseId, string formType, string? requiredStatus, CancellationToken ct)
    {
        var query = _dbContext.CaseForms.AsQueryable()
            .Where(x => x.CaseId == caseId && x.FormType == formType);

        if (!string.IsNullOrWhiteSpace(requiredStatus))
        {
            query = query.Where(x => x.Status == requiredStatus);
        }

        return query.AnyAsync(ct);
    }

    public Task<bool> PlanVersionExistsAsync(Guid caseId, CancellationToken ct)
    {
        return _dbContext.PlanVersions.AnyAsync(x => x.CaseId == caseId, ct);
    }

    public async Task UpdateCaseAsync(CaseData item, CancellationToken ct)
    {
        var entity = await _dbContext.Cases.FindAsync([item.CaseId], ct)
            ?? throw new InvalidOperationException($"Case '{item.CaseId}' not found.");

        entity.CurrentStatus = item.CurrentStatus;
        entity.StatusVersion = item.StatusVersion;
        entity.CtStudyInstanceUid = item.CtStudyInstanceUid;
        entity.CtWadoRsUrl = item.CtWadoRsUrl;
        entity.PvMedJobId = item.PvMedJobId;
        entity.RtStructSeriesInstanceUid = item.RtStructSeriesInstanceUid;
        entity.Notes = item.Notes;
        entity.CurrentPlannerUserId = item.CurrentPlannerUserId;
        entity.CurrentReviewerUserId = item.CurrentReviewerUserId;
        entity.CurrentPlanVersionNo = item.CurrentPlanVersionNo;
        entity.UpdatedAt = item.UpdatedAt;
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
            Notes = item.Notes,
            CurrentPlannerUserId = item.CurrentPlannerUserId,
            CurrentReviewerUserId = item.CurrentReviewerUserId,
            CurrentPlanVersionNo = item.CurrentPlanVersionNo,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        }, ct);
    }

    public async Task AddCaseFormAsync(CaseFormData item, CancellationToken ct)
    {
        await _dbContext.CaseForms.AddAsync(new CaseFormEntity
        {
            FormId = item.FormId,
            CaseId = item.CaseId,
            FormType = item.FormType,
            FormVersion = item.FormVersion,
            Status = item.Status,
            PayloadJson = item.PayloadJson,
            SubmittedBy = item.SubmittedBy,
            SubmittedAt = item.SubmittedAt,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        }, ct);
    }

    public async Task UpdateCaseFormAsync(CaseFormData item, CancellationToken ct)
    {
        var entity = await _dbContext.CaseForms.FirstOrDefaultAsync(x => x.FormId == item.FormId, ct)
            ?? throw new InvalidOperationException($"Case form '{item.FormId}' not found.");

        entity.PayloadJson = item.PayloadJson;
        entity.Status = item.Status;
        entity.SubmittedBy = item.SubmittedBy;
        entity.SubmittedAt = item.SubmittedAt;
        entity.UpdatedAt = item.UpdatedAt;
    }

    public async Task AddWorkItemAsync(WorkItemData item, CancellationToken ct)
    {
        await _dbContext.WorkItems.AddAsync(new WorkItemEntity
        {
            WorkItemId = item.WorkItemId,
            CaseId = item.CaseId,
            SequenceNo = item.SequenceNo,
            ParentWorkItemId = item.ParentWorkItemId,
            Type = item.Type,
            Status = item.Status,
            WorkItemGroup = item.WorkItemGroup,
            AssignedRole = item.AssignedRole,
            AssignedUserId = item.AssignedUserId,
            DueAt = item.DueAt,
            SlaMinutes = item.SlaMinutes,
            ExternalCorrelationId = item.ExternalCorrelationId,
            ResultCode = item.ResultCode,
            CompletedAt = item.CompletedAt,
            CompletedBy = item.CompletedBy,
            FormId = item.FormId,
            RequiresDifferentUserFrom = item.RequiresDifferentUserFrom,
            RetryCount = item.RetryCount,
            Remarks = item.Remarks,
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
            LastTriedAt = item.LastTriedAt,
            MessageType = item.MessageType,
            SchemaVersion = item.SchemaVersion,
            CorrelationId = item.CorrelationId,
            Traceparent = item.Traceparent,
            DeliveryMode = item.DeliveryMode
        }, ct);
    }

    public Task EnqueueOutboxAsync(Guid? caseId, string targetSystem, string action, string payloadJson, CancellationToken ct) =>
        EnqueueOutboxAsync(
            caseId, targetSystem, action, payloadJson,
            messageType: null,
            schemaVersion: 1,
            correlationId: caseId,
            traceparent: Wfmgr.Application.Diagnostics.WfmgrActivitySource.CurrentTraceparent(),
            deliveryMode: Wfmgr.Domain.Integrations.OutboxDeliveryMode.Http,
            ct);

    public async Task EnqueueOutboxAsync(
        Guid? caseId,
        string targetSystem,
        string action,
        string payloadJson,
        string? messageType,
        int schemaVersion,
        Guid? correlationId,
        string? traceparent,
        Wfmgr.Domain.Integrations.OutboxDeliveryMode deliveryMode,
        CancellationToken ct)
    {
        await AddOutboxMessageAsync(new OutboxMessageData
        {
            MessageId = Guid.NewGuid(),
            CaseId = caseId,
            TargetSystem = targetSystem,
            Action = action,
            PayloadJson = payloadJson,
            Status = Domain.Enums.OutboxStatus.New,
            RetryCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            MessageType = messageType,
            SchemaVersion = schemaVersion,
            CorrelationId = correlationId,
            Traceparent = traceparent,
            DeliveryMode = deliveryMode
        }, ct);
    }

    public async Task<bool> TryReserveExternalEventInboxAsync(
        string integration,
        string externalEventId,
        string? messageType,
        string? payloadHash,
        Guid? caseId,
        string? traceparent,
        CancellationToken ct)
    {
        var existing = await _dbContext.ExternalEventInbox
            .AsNoTracking()
            .AnyAsync(x => x.Integration == integration && x.ExternalEventId == externalEventId, ct);
        if (existing) return false;

        await _dbContext.ExternalEventInbox.AddAsync(new ExternalEventInboxEntity
        {
            Integration = integration,
            ExternalEventId = externalEventId,
            MessageType = messageType,
            PayloadHash = payloadHash,
            CaseId = caseId,
            Traceparent = traceparent,
            ReceivedAt = DateTimeOffset.UtcNow
        }, ct);

        // Note: the unique (Integration, ExternalEventId) primary key still guards us against
        // a concurrent insert race; the caller's SaveChangesAsync will surface that as a
        // DbUpdateException which they should treat as a duplicate.
        return true;
    }

    public async Task MarkExternalEventInboxProcessedAsync(
        string integration,
        string externalEventId,
        Guid? caseId,
        CancellationToken ct)
    {
        var row = await _dbContext.ExternalEventInbox
            .FirstOrDefaultAsync(x => x.Integration == integration && x.ExternalEventId == externalEventId, ct);
        if (row is null) return;
        row.ProcessedAt = DateTimeOffset.UtcNow;
        if (caseId is not null) row.CaseId = caseId;
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

    public async Task AddCaseTransitionHistoryAsync(CaseTransitionHistoryData item, CancellationToken ct)
    {
        await _dbContext.CaseTransitionHistories.AddAsync(new CaseTransitionHistoryEntity
        {
            TransitionId = item.TransitionId,
            CaseId = item.CaseId,
            FromStatus = item.FromStatus,
            ToStatus = item.ToStatus,
            TriggerType = item.TriggerType,
            TriggerName = item.TriggerName,
            TriggeredBy = item.TriggeredBy,
            Reason = item.Reason,
            MetadataJson = item.MetadataJson,
            CreatedAt = item.CreatedAt
        }, ct);
    }

    public async Task UpsertIntegrationReferenceAsync(
        Guid caseId,
        string systemName,
        string externalEntityType,
        string externalId,
        string? externalStatus,
        string? metadataJson,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var entity = await _dbContext.IntegrationReferences.FirstOrDefaultAsync(
            x => x.CaseId == caseId
                && x.SystemName == systemName
                && x.ExternalEntityType == externalEntityType
                && x.ExternalId == externalId,
            ct);

        if (entity is null)
        {
            await _dbContext.IntegrationReferences.AddAsync(new IntegrationReferenceEntity
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                SystemName = systemName,
                ExternalEntityType = externalEntityType,
                ExternalId = externalId,
                ExternalStatus = externalStatus,
                MetadataJson = metadataJson,
                CreatedAt = now,
                UpdatedAt = now
            }, ct);

            return;
        }

        entity.ExternalStatus = externalStatus;
        entity.MetadataJson = metadataJson;
        entity.UpdatedAt = now;
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
            Notes = entity.Notes,
            CurrentPlannerUserId = entity.CurrentPlannerUserId,
            CurrentReviewerUserId = entity.CurrentReviewerUserId,
            CurrentPlanVersionNo = entity.CurrentPlanVersionNo,
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
            SequenceNo = entity.SequenceNo,
            ParentWorkItemId = entity.ParentWorkItemId,
            Type = entity.Type,
            Status = entity.Status,
            WorkItemGroup = entity.WorkItemGroup,
            AssignedRole = entity.AssignedRole,
            AssignedUserId = entity.AssignedUserId,
            DueAt = entity.DueAt,
            SlaMinutes = entity.SlaMinutes,
            ExternalCorrelationId = entity.ExternalCorrelationId,
            ResultCode = entity.ResultCode,
            CompletedAt = entity.CompletedAt,
            CompletedBy = entity.CompletedBy,
            FormId = entity.FormId,
            RequiresDifferentUserFrom = entity.RequiresDifferentUserFrom,
            RetryCount = entity.RetryCount,
            Remarks = entity.Remarks,
            PayloadJson = entity.PayloadJson,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

        _trackedWorkItems[entity.WorkItemId] = mapped;
        return mapped;
    }

    private static CaseFormData Map(CaseFormEntity entity)
    {
        return new CaseFormData
        {
            FormId = entity.FormId,
            CaseId = entity.CaseId,
            FormType = entity.FormType,
            FormVersion = entity.FormVersion,
            Status = entity.Status,
            PayloadJson = entity.PayloadJson,
            SubmittedBy = entity.SubmittedBy,
            SubmittedAt = entity.SubmittedAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
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
            entity.Notes = tracked.Notes;
            entity.CurrentPlannerUserId = tracked.CurrentPlannerUserId;
            entity.CurrentReviewerUserId = tracked.CurrentReviewerUserId;
            entity.CurrentPlanVersionNo = tracked.CurrentPlanVersionNo;
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
            entity.SequenceNo = tracked.SequenceNo;
            entity.ParentWorkItemId = tracked.ParentWorkItemId;
            entity.Status = tracked.Status;
            entity.WorkItemGroup = tracked.WorkItemGroup;
            entity.AssignedRole = tracked.AssignedRole;
            entity.AssignedUserId = tracked.AssignedUserId;
            entity.DueAt = tracked.DueAt;
            entity.SlaMinutes = tracked.SlaMinutes;
            entity.ExternalCorrelationId = tracked.ExternalCorrelationId;
            entity.ResultCode = tracked.ResultCode;
            entity.CompletedAt = tracked.CompletedAt;
            entity.CompletedBy = tracked.CompletedBy;
            entity.FormId = tracked.FormId;
            entity.RequiresDifferentUserFrom = tracked.RequiresDifferentUserFrom;
            entity.RetryCount = tracked.RetryCount;
            entity.Remarks = tracked.Remarks;
            entity.PayloadJson = tracked.PayloadJson;
            entity.UpdatedAt = tracked.UpdatedAt;
        }
    }
}
