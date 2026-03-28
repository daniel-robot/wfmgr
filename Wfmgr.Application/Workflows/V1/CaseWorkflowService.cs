using System.Text.Json;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Application.Workflows.V1.Dtos;
using Wfmgr.Domain.Enums;

namespace Wfmgr.Application.Workflows.V1;

public class CaseWorkflowService : ICaseWorkflowService
{
    private const string SimRecordType = "SIM_RECORD";
    private const string ManualContouringType = "MANUAL_CONTOURING";
    private const string ManualForwardToMonacoType = "MANUAL_FORWARD_TO_MONACO";

    private readonly IWorkflowDataAccess _dataAccess;
    private readonly IWorkflowProfileResolver _profileResolver;

    public CaseWorkflowService(IWorkflowDataAccess dataAccess, IWorkflowProfileResolver profileResolver)
    {
        _dataAccess = dataAccess;
        _profileResolver = profileResolver;
    }

    public async Task<Guid> CreateCaseAsync(CreateCaseRequest request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var caseId = Guid.NewGuid();

        var item = new CaseData
        {
            CaseId = caseId,
            HospitalId = request.HospitalId,
            SiteId = request.SiteId,
            DepartmentId = request.DepartmentId,
            PatientId = request.PatientId,
            AccessionNumber = request.AccessionNumber,
            CurrentStatus = CaseStatus.Submitted,
            StatusVersion = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _dataAccess.AddCaseAsync(item, ct);

        await _dataAccess.AddWorkItemAsync(new WorkItemData
        {
            WorkItemId = Guid.NewGuid(),
            CaseId = caseId,
            Type = SimRecordType,
            Status = WorkItemStatus.Pending,
            AssignedRole = "SimTech",
            PayloadJson = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        }, ct);

        await AddAuditAsync(caseId, "CreateCase", null, CaseStatus.Submitted, request, ct);
        await _dataAccess.SaveChangesAsync(ct);

        return caseId;
    }

    public async Task SubmitSimRecordAsync(Guid caseId, SubmitSimRecordRequest request, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        if (caseData.CurrentStatus != CaseStatus.Submitted)
        {
            throw new InvalidOperationException("Case must be in Submitted status.");
        }

        var now = DateTimeOffset.UtcNow;
        var simWorkItem = await _dataAccess.GetOpenWorkItemAsync(caseId, SimRecordType, ct);
        if (simWorkItem is not null)
        {
            simWorkItem.Status = WorkItemStatus.Done;
            simWorkItem.UpdatedAt = now;
        }

        var fromStatus = caseData.CurrentStatus;
        caseData.CurrentStatus = CaseStatus.SimCompleted;
        caseData.StatusVersion += 1;
        caseData.UpdatedAt = now;

        await AddAuditAsync(caseId, "SubmitSimRecord", fromStatus, CaseStatus.SimCompleted, request, ct);
        await _dataAccess.SaveChangesAsync(ct);
    }

    public async Task HandleCtImageStoredAsync(CtImageStoredRequest request, CancellationToken ct)
    {
        if (await _dataAccess.ExternalEventExistsAsync("CT", "IMAGE_STORED", request.ExternalEventId, ct))
        {
            return;
        }

        var caseData = await _dataAccess.GetCaseByAccessionNumberAsync(request.AccessionNumber, ct)
            ?? throw new InvalidOperationException("Case not found by accession number.");

        if (caseData.CurrentStatus != CaseStatus.SimCompleted)
        {
            throw new InvalidOperationException("Case must be in SimCompleted status.");
        }

        var now = DateTimeOffset.UtcNow;
        caseData.CtStudyInstanceUid = request.DicomRef.StudyInstanceUid;
        caseData.CtWadoRsUrl = request.DicomWebLocation.WadoRsUrl;

        var fromStatus = caseData.CurrentStatus;
        caseData.CurrentStatus = CaseStatus.ImageStored;
        caseData.StatusVersion += 1;

        var strategy = await _profileResolver.ResolveS1ContouringStrategyAsync(
            caseData.HospitalId,
            caseData.SiteId,
            caseData.DepartmentId,
            ct);

        if (strategy.AutoContourEnabled)
        {
            await _dataAccess.AddOutboxMessageAsync(new OutboxMessageData
            {
                MessageId = Guid.NewGuid(),
                CaseId = caseData.CaseId,
                TargetSystem = "PvMed",
                Action = "SEND_TO_PVMED_AUTOCONTOUR",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    caseData.CaseId,
                    caseData.AccessionNumber,
                    request.DicomRef,
                    request.DicomWebLocation
                }),
                Status = OutboxStatus.New,
                RetryCount = 0,
                CreatedAt = now
            }, ct);
        }
        else
        {
            await _dataAccess.AddWorkItemAsync(new WorkItemData
            {
                WorkItemId = Guid.NewGuid(),
                CaseId = caseData.CaseId,
                Type = ManualContouringType,
                Status = WorkItemStatus.Pending,
                AssignedRole = "Dosimetrist",
                CreatedAt = now,
                UpdatedAt = now
            }, ct);
        }

        caseData.CurrentStatus = CaseStatus.ContouringInProgress;
        caseData.StatusVersion += 1;
        caseData.UpdatedAt = now;

        await _dataAccess.AddExternalEventAsync(new ExternalEventData
        {
            EventId = Guid.NewGuid(),
            Source = "CT",
            Type = "IMAGE_STORED",
            ExternalId = request.ExternalEventId,
            CaseCorrelationKey = request.AccessionNumber,
            CaseId = caseData.CaseId,
            PayloadJson = JsonSerializer.Serialize(request),
            ReceivedAt = request.OccurredAt,
            ProcessedAt = now,
            ProcessStatus = "Processed"
        }, ct);

        await AddAuditAsync(caseData.CaseId, "HandleCtImageStored", fromStatus, CaseStatus.ContouringInProgress, request, ct);
        await _dataAccess.SaveChangesAsync(ct);
    }

    public async Task HandlePvMedEventAsync(PvMedEventRequest request, CancellationToken ct)
    {
        if (await _dataAccess.ExternalEventExistsAsync("PVMED", request.Type, request.ExternalEventId, ct))
        {
            return;
        }

        var caseData = await _dataAccess.GetCaseByIdAsync(request.CaseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        var now = DateTimeOffset.UtcNow;
        caseData.PvMedJobId = request.PvMedJob.JobId;

        var strategy = await _profileResolver.ResolveS1ContouringStrategyAsync(
            caseData.HospitalId,
            caseData.SiteId,
            caseData.DepartmentId,
            ct);

        if (string.Equals(request.Type, "PVMED_AUTOCONTOUR_COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            if (caseData.CurrentStatus != CaseStatus.ContouringInProgress)
            {
                throw new InvalidOperationException("Case must be in ContouringInProgress status.");
            }

            var fromStatus = caseData.CurrentStatus;
            caseData.RtStructSeriesInstanceUid = request.PvMedResult?.RtStructLocation.SeriesInstanceUid;
            caseData.CurrentStatus = CaseStatus.ContoursReady;
            caseData.StatusVersion += 1;

            if (strategy.OnAutoContourComplete.AutoForwardToMonaco)
            {
                await _dataAccess.AddOutboxMessageAsync(new OutboxMessageData
                {
                    MessageId = Guid.NewGuid(),
                    CaseId = caseData.CaseId,
                    TargetSystem = "Monaco",
                    Action = "SEND_TO_MONACO_IMPORT",
                    PayloadJson = JsonSerializer.Serialize(new
                    {
                        caseData.CaseId,
                        request.PvMedResult,
                        caseData.AccessionNumber
                    }),
                    Status = OutboxStatus.New,
                    RetryCount = 0,
                    CreatedAt = now
                }, ct);

                caseData.CurrentStatus = CaseStatus.MonacoForwarded;
                caseData.StatusVersion += 1;
                await AddAuditAsync(caseData.CaseId, "AutoForwardToMonaco", CaseStatus.ContoursReady, CaseStatus.MonacoForwarded, request, ct);
            }
            else
            {
                await _dataAccess.AddWorkItemAsync(new WorkItemData
                {
                    WorkItemId = Guid.NewGuid(),
                    CaseId = caseData.CaseId,
                    Type = ManualForwardToMonacoType,
                    Status = WorkItemStatus.Pending,
                    AssignedRole = "Dosimetrist",
                    CreatedAt = now,
                    UpdatedAt = now
                }, ct);
            }

            caseData.UpdatedAt = now;
            await AddAuditAsync(caseData.CaseId, "HandlePvMedCompleted", fromStatus, caseData.CurrentStatus, request, ct);
        }
        else if (string.Equals(request.Type, "PVMED_AUTOCONTOUR_FAILED", StringComparison.OrdinalIgnoreCase))
        {
            if (strategy.Fallback.OnFailureCreateManualWorkItem)
            {
                await _dataAccess.AddWorkItemAsync(new WorkItemData
                {
                    WorkItemId = Guid.NewGuid(),
                    CaseId = caseData.CaseId,
                    Type = ManualContouringType,
                    Status = WorkItemStatus.Pending,
                    AssignedRole = strategy.Fallback.ManualWorkItemRole,
                    CreatedAt = now,
                    UpdatedAt = now
                }, ct);
            }

            await AddAuditAsync(caseData.CaseId, "HandlePvMedFailed", null, null, request, ct);
        }
        else
        {
            await AddAuditAsync(caseData.CaseId, "HandlePvMedProgress", null, null, request, ct);
        }

        await _dataAccess.AddExternalEventAsync(new ExternalEventData
        {
            EventId = Guid.NewGuid(),
            Source = "PVMED",
            Type = request.Type,
            ExternalId = request.ExternalEventId,
            CaseCorrelationKey = caseData.AccessionNumber,
            CaseId = caseData.CaseId,
            PayloadJson = JsonSerializer.Serialize(request),
            ReceivedAt = request.OccurredAt,
            ProcessedAt = now,
            ProcessStatus = "Processed"
        }, ct);

        await _dataAccess.SaveChangesAsync(ct);
    }

    public async Task ForwardToMonacoAsync(Guid caseId, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        if (caseData.CurrentStatus != CaseStatus.ContoursReady)
        {
            throw new InvalidOperationException("Case must be in ContoursReady status.");
        }

        var now = DateTimeOffset.UtcNow;
        await _dataAccess.AddOutboxMessageAsync(new OutboxMessageData
        {
            MessageId = Guid.NewGuid(),
            CaseId = caseData.CaseId,
            TargetSystem = "Monaco",
            Action = "SEND_TO_MONACO_IMPORT",
            PayloadJson = JsonSerializer.Serialize(new
            {
                caseData.CaseId,
                caseData.AccessionNumber,
                caseData.CtStudyInstanceUid,
                caseData.RtStructSeriesInstanceUid
            }),
            Status = OutboxStatus.New,
            RetryCount = 0,
            CreatedAt = now
        }, ct);

        var fromStatus = caseData.CurrentStatus;
        caseData.CurrentStatus = CaseStatus.MonacoForwarded;
        caseData.StatusVersion += 1;
        caseData.UpdatedAt = now;

        await AddAuditAsync(caseData.CaseId, "ForwardToMonaco", fromStatus, CaseStatus.MonacoForwarded, null, ct);
        await _dataAccess.SaveChangesAsync(ct);
    }

    private async Task AddAuditAsync(
        Guid caseId,
        string action,
        CaseStatus? fromStatus,
        CaseStatus? toStatus,
        object? snapshot,
        CancellationToken ct)
    {
        await _dataAccess.AddAuditLogAsync(new AuditLogData
        {
            AuditId = Guid.NewGuid(),
            CaseId = caseId,
            ActorType = "System",
            Action = action,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            SnapshotJson = snapshot is null ? "{}" : JsonSerializer.Serialize(snapshot),
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);
    }
}
