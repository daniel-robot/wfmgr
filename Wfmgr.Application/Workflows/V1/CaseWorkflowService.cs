using System.Text.Json;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Application.Workflows.V1.Dtos;
using Wfmgr.Application.Workflows.V1.StateMachine;
using Wfmgr.Application.Workflows.V1.WorkItems;
using Wfmgr.Domain.Enums;
using Wfmgr.Domain.Integrations;
using Wfmgr.Domain.WorkItems;

namespace Wfmgr.Application.Workflows.V1;

public class CaseWorkflowService : ICaseWorkflowService
{
    private readonly IWorkflowDataAccess _dataAccess;
    private readonly IWorkflowProfileResolver _profileResolver;
    private readonly IWorkItemLifecycleService _workItemLifecycleService;
    private readonly ICaseStateMachineService _caseStateMachineService;

    public CaseWorkflowService(
        IWorkflowDataAccess dataAccess,
        IWorkflowProfileResolver profileResolver,
        IWorkItemLifecycleService workItemLifecycleService,
        ICaseStateMachineService caseStateMachineService)
    {
        _dataAccess = dataAccess;
        _profileResolver = profileResolver;
        _workItemLifecycleService = workItemLifecycleService;
        _caseStateMachineService = caseStateMachineService;
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
            CurrentStatus = CaseStatus.Draft,
            StatusVersion = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _dataAccess.AddCaseAsync(item, ct);

        await _caseStateMachineService.ApplyTransitionAsync(item, CaseStatus.Submitted, new TransitionExecutionContext
        {
            TriggerName = "SubmitCase",
            TriggerType = WorkflowTriggerType.User,
            TriggeredBy = "System",
            ActorRoles = ["Coordinator"],
            Metadata = request
        }, ct);

        await _workItemLifecycleService.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
        {
            CaseId = caseId,
            Type = WorkItemTypes.SimulationRecord,
            AssignedRole = "SimTech",
            PayloadJson = request.Notes,
            CreatedAtUtc = now
        }, ct);

        await _dataAccess.SaveChangesAsync(ct);

        return caseId;
    }

    public async Task SubmitSimRecordAsync(Guid caseId, SubmitSimRecordRequest request, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        if (caseData.CurrentStatus == CaseStatus.SimCompleted)
        {
            return;
        }

        // Idempotency: if the case already progressed beyond simulation, treat this as a no-op.
        if (caseData.CurrentStatus > CaseStatus.SimCompleted)
        {
            return;
        }

        if (caseData.CurrentStatus is not (CaseStatus.Draft or CaseStatus.Submitted or CaseStatus.SimScheduled or CaseStatus.SimInProgress))
        {
            throw new InvalidOperationException($"Case must be in Draft, Submitted, SimScheduled, or SimInProgress status. Current status is '{caseData.CurrentStatus}'.");
        }

        var now = DateTimeOffset.UtcNow;
        var simWorkItem = await _dataAccess.GetOpenWorkItemAsync(caseId, WorkItemTypes.SimulationRecord, ct);
        if (simWorkItem is not null)
        {
            _workItemLifecycleService.CompleteWorkItem(simWorkItem, completedBy: "SimTech", resultCode: "Recorded", completedAtUtc: now);
        }

        if (caseData.CurrentStatus == CaseStatus.Draft)
        {
            await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.Submitted, new TransitionExecutionContext
            {
                TriggerName = "SubmitCase",
                TriggerType = WorkflowTriggerType.User,
                TriggeredBy = "SimTech",
                ActorRoles = ["SimTech"],
                Metadata = request
            }, ct);
        }

        if (caseData.CurrentStatus == CaseStatus.Submitted)
        {
            await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.SimScheduled, new TransitionExecutionContext
            {
                TriggerName = "ScheduleSimulation",
                TriggerType = WorkflowTriggerType.User,
                TriggeredBy = "SimTech",
                ActorRoles = ["SimTech"],
                Metadata = request
            }, ct);
        }

        if (caseData.CurrentStatus == CaseStatus.SimScheduled)
        {
            await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.SimInProgress, new TransitionExecutionContext
            {
                TriggerName = "StartSimulation",
                TriggerType = WorkflowTriggerType.User,
                TriggeredBy = "SimTech",
                ActorRoles = ["SimTech"],
                Metadata = request
            }, ct);
        }

        if (caseData.CurrentStatus == CaseStatus.SimInProgress)
        {
            await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.SimCompleted, new TransitionExecutionContext
            {
                TriggerName = "CompleteSimulation",
                TriggerType = WorkflowTriggerType.User,
                TriggeredBy = "SimTech",
                ActorRoles = ["SimTech"],
                Metadata = request
            }, ct);
        }

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

        // Idempotency: if the case already progressed beyond image intake, ignore duplicate/late CT events.
        if (caseData.CurrentStatus >= CaseStatus.ImageStored)
        {
            return;
        }

        if (caseData.CurrentStatus != CaseStatus.SimCompleted)
        {
            throw new InvalidOperationException($"Case must be in SimCompleted status. Current status is '{caseData.CurrentStatus}'.");
        }

        var now = DateTimeOffset.UtcNow;
        caseData.CtStudyInstanceUid = request.DicomRef.StudyInstanceUid;
        caseData.CtWadoRsUrl = request.DicomWebLocation.WadoRsUrl;

        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.ImageStored, new TransitionExecutionContext
        {
            TriggerName = "StoreImage",
            TriggerType = WorkflowTriggerType.ExternalEvent,
            TriggeredBy = "CT",
            Metadata = request
        }, ct);

        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.ImageForwarding, new TransitionExecutionContext
        {
            TriggerName = "ForwardImage",
            TriggerType = WorkflowTriggerType.System,
            TriggeredBy = "System",
            Metadata = request
        }, ct);

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
                Action = OutboxActions.SendImagesToContourTool,
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

            await _workItemLifecycleService.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
            {
                CaseId = caseData.CaseId,
                Type = WorkItemTypes.AutoContourMonitor,
                AssignedRole = "Dosimetrist",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    request.DicomRef,
                    request.DicomWebLocation
                }),
                CreatedAtUtc = now
            }, ct);
        }
        else
        {
            await _workItemLifecycleService.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
            {
                CaseId = caseData.CaseId,
                Type = WorkItemTypes.ManualContouring,
                AssignedRole = "Dosimetrist",
                CreatedAtUtc = now
            }, ct);
        }

        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.ContouringInProgress, new TransitionExecutionContext
        {
            TriggerName = "StartContouring",
            TriggerType = WorkflowTriggerType.System,
            TriggeredBy = "System",
            Metadata = request
        }, ct);

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

            caseData.RtStructSeriesInstanceUid = request.PvMedResult?.RtStructLocation.SeriesInstanceUid;

            var autoContourMonitor = await _dataAccess.GetOpenWorkItemAsync(caseData.CaseId, WorkItemTypes.AutoContourMonitor, ct);
            if (autoContourMonitor is not null)
            {
                _workItemLifecycleService.CompleteWorkItem(
                    autoContourMonitor,
                    completedBy: "PVMED",
                    resultCode: WorkItemResultCodes.Approved,
                    completedAtUtc: now);
            }

            await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.ContoursReady, new TransitionExecutionContext
            {
                TriggerName = "ContoursReady",
                TriggerType = WorkflowTriggerType.ExternalEvent,
                TriggeredBy = "PVMED",
                Metadata = request
            }, ct);

            if (strategy.OnAutoContourComplete.AutoForwardToMonaco)
            {
                await _dataAccess.AddOutboxMessageAsync(new OutboxMessageData
                {
                    MessageId = Guid.NewGuid(),
                    CaseId = caseData.CaseId,
                    TargetSystem = "Monaco",
                    Action = OutboxActions.SendToMonacoImport,
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

                var hasContourApproval = await _dataAccess.WorkItemExistsAsync(caseData.CaseId, WorkItemTypes.ContourReview, "Approved", ct);
                if (!hasContourApproval)
                {
                    var contourReview = await _workItemLifecycleService.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
                    {
                        CaseId = caseData.CaseId,
                        Type = WorkItemTypes.ContourReview,
                        AssignedRole = "Physician",
                        CreatedAtUtc = now
                    }, ct);

                    _workItemLifecycleService.CompleteWorkItem(
                        contourReview,
                        completedBy: "System",
                        resultCode: WorkItemResultCodes.Approved,
                        completedAtUtc: now);
                }

                await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.ContoursUnderReview, new TransitionExecutionContext
                {
                    TriggerName = "StartContourReview",
                    TriggerType = WorkflowTriggerType.System,
                    TriggeredBy = "System",
                    ActorRoles = ["Physician"],
                    Metadata = request
                }, ct);

                await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PlanningPending, new TransitionExecutionContext
                {
                    TriggerName = "ApproveContours",
                    TriggerType = WorkflowTriggerType.System,
                    TriggeredBy = "System",
                    ActorRoles = ["Physician"],
                    Metadata = request
                }, ct);
            }
            else
            {
                await _workItemLifecycleService.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
                {
                    CaseId = caseData.CaseId,
                    Type = WorkItemTypes.ManualForwardToMonaco,
                    AssignedRole = "Dosimetrist",
                    CreatedAtUtc = now
                }, ct);
            }
        }
        else if (string.Equals(request.Type, "PVMED_AUTOCONTOUR_FAILED", StringComparison.OrdinalIgnoreCase))
        {
            if (caseData.CurrentStatus == CaseStatus.ContouringInProgress)
            {
                var autoContourMonitor = await _dataAccess.GetOpenWorkItemAsync(caseData.CaseId, WorkItemTypes.AutoContourMonitor, ct);
                if (autoContourMonitor is not null)
                {
                    _workItemLifecycleService.RejectWorkItem(
                        autoContourMonitor,
                        completedBy: "PVMED",
                        resultCode: WorkItemResultCodes.Failed,
                        remarks: request.Type,
                        completedAtUtc: now);
                }

                await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.ContourReworkRequired, new TransitionExecutionContext
                {
                    TriggerName = "AutoContourFailed",
                    TriggerType = WorkflowTriggerType.ExternalEvent,
                    TriggeredBy = "PVMED",
                    Reason = request.Type,
                    ActorRoles = ["Physician"],
                    Metadata = request
                }, ct);

                await _workItemLifecycleService.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
                {
                    CaseId = caseData.CaseId,
                    ParentWorkItemId = autoContourMonitor?.WorkItemId,
                    Type = WorkItemTypes.ContourRework,
                    AssignedRole = strategy.Fallback.ManualWorkItemRole,
                    PayloadJson = JsonSerializer.Serialize(new { request.Type, request.ExternalEventId }),
                    CreatedAtUtc = now
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

    public async Task RestartContouringAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        var toStatus = caseData.CurrentStatus switch
        {
            CaseStatus.ContourReworkRequired => CaseStatus.ContouringInProgress,
            CaseStatus.ContoursRejected => CaseStatus.ContouringInProgress,
            _ => throw new InvalidOperationException("Case must be in ContourReworkRequired or ContoursRejected status.")
        };

        await _caseStateMachineService.ApplyTransitionAsync(caseData, toStatus, new TransitionExecutionContext
        {
            TriggerName = caseData.CurrentStatus == CaseStatus.ContourReworkRequired ? "RestartContouring" : "ReopenContouring",
            TriggerType = WorkflowTriggerType.User,
            TriggeredBy = triggeredBy,
            ActorRoles = ["Dosimetrist", "Physician"],
            Reason = reason,
            Metadata = new { caseId, reason }
        }, ct);

        await _dataAccess.SaveChangesAsync(ct);
    }

    public async Task RejectContourReviewAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        if (caseData.CurrentStatus != CaseStatus.ContoursUnderReview)
        {
            throw new InvalidOperationException("Case must be in ContoursUnderReview status.");
        }

        var now = DateTimeOffset.UtcNow;
        var review = await _dataAccess.GetOpenWorkItemAsync(caseId, WorkItemTypes.ContourReview, ct);
        if (review is not null)
        {
            _workItemLifecycleService.RejectWorkItem(
                review,
                completedBy: triggeredBy,
                resultCode: WorkItemResultCodes.Rejected,
                remarks: reason,
                completedAtUtc: now);
        }

        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.ContoursRejected, new TransitionExecutionContext
        {
            TriggerName = "RejectContours",
            TriggerType = WorkflowTriggerType.User,
            TriggeredBy = triggeredBy,
            ActorRoles = ["Physician"],
            Reason = reason,
            Metadata = new { caseId, reason }
        }, ct);

        await _workItemLifecycleService.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
        {
            CaseId = caseId,
            ParentWorkItemId = review?.WorkItemId,
            Type = WorkItemTypes.ContourRework,
            AssignedRole = "Dosimetrist",
            PayloadJson = JsonSerializer.Serialize(new { reason }),
            CreatedAtUtc = now
        }, ct);

        await _dataAccess.SaveChangesAsync(ct);
    }

    public async Task RejectPlanReviewAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        if (caseData.CurrentStatus != CaseStatus.PlanUnderReview)
        {
            throw new InvalidOperationException("Case must be in PlanUnderReview status.");
        }

        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PlanningInProgress, new TransitionExecutionContext
        {
            TriggerName = "RequestPlanChanges",
            TriggerType = WorkflowTriggerType.User,
            TriggeredBy = triggeredBy,
            ActorRoles = ["Physician"],
            Reason = reason,
            Metadata = new { caseId, reason }
        }, ct);

        await _workItemLifecycleService.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
        {
            CaseId = caseId,
            Type = WorkItemTypes.PlanDesign,
            AssignedRole = "Dosimetrist",
            PayloadJson = JsonSerializer.Serialize(new { reason, rework = true }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, ct);

        await _dataAccess.SaveChangesAsync(ct);
    }

    public async Task RejectPlanReReviewAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        if (caseData.CurrentStatus != CaseStatus.PlanReReviewOptional)
        {
            throw new InvalidOperationException("Case must be in PlanReReviewOptional status.");
        }

        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PlanningInProgress, new TransitionExecutionContext
        {
            TriggerName = "ReturnToPlanning",
            TriggerType = WorkflowTriggerType.User,
            TriggeredBy = triggeredBy,
            Reason = reason,
            Metadata = new { caseId, reason }
        }, ct);

        await _workItemLifecycleService.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
        {
            CaseId = caseId,
            Type = WorkItemTypes.PlanReReview,
            AssignedRole = "Physician",
            PayloadJson = JsonSerializer.Serialize(new { reason, rejected = true }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, ct);

        await _dataAccess.SaveChangesAsync(ct);
    }

    public async Task HandlePrescriptionSyncFailureAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        if (caseData.CurrentStatus != CaseStatus.PrescriptionGenerating)
        {
            throw new InvalidOperationException("Case must be in PrescriptionGenerating status.");
        }

        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PrescriptionSyncFailed, new TransitionExecutionContext
        {
            TriggerName = "PrescriptionSyncFailed",
            TriggerType = WorkflowTriggerType.ExternalEvent,
            TriggeredBy = triggeredBy,
            Reason = reason,
            Metadata = new { caseId, reason }
        }, ct);

        await _workItemLifecycleService.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
        {
            CaseId = caseId,
            Type = WorkItemTypes.PrescriptionSync,
            AssignedRole = "Physician",
            PayloadJson = JsonSerializer.Serialize(new { reason, manualRetry = true }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, ct);

        await _dataAccess.SaveChangesAsync(ct);
    }

    public async Task RetryPrescriptionSyncAsync(Guid caseId, string triggeredBy, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        if (caseData.CurrentStatus != CaseStatus.PrescriptionSyncFailed)
        {
            throw new InvalidOperationException("Case must be in PrescriptionSyncFailed status.");
        }

        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PrescriptionGenerating, new TransitionExecutionContext
        {
            TriggerName = "RetryPrescriptionSync",
            TriggerType = WorkflowTriggerType.User,
            TriggeredBy = triggeredBy,
            ActorRoles = ["Physician", "Dosimetrist"]
        }, ct);

        await _dataAccess.SaveChangesAsync(ct);
    }

    public async Task ResolvePrescriptionSyncAsync(Guid caseId, string triggeredBy, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        if (caseData.CurrentStatus != CaseStatus.PrescriptionSyncFailed)
        {
            throw new InvalidOperationException("Case must be in PrescriptionSyncFailed status.");
        }

        var now = DateTimeOffset.UtcNow;
        var syncWorkItem = await _dataAccess.GetOpenWorkItemAsync(caseId, WorkItemTypes.PrescriptionSync, ct);
        if (syncWorkItem is not null)
        {
            _workItemLifecycleService.CompleteWorkItem(
                syncWorkItem,
                completedBy: triggeredBy,
                resultCode: WorkItemResultCodes.Synced,
                completedAtUtc: now);
        }

        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PrescriptionReady, new TransitionExecutionContext
        {
            TriggerName = "ResolvePrescriptionSync",
            TriggerType = WorkflowTriggerType.User,
            TriggeredBy = triggeredBy,
            ActorRoles = ["Physician", "Dosimetrist"]
        }, ct);

        await _dataAccess.SaveChangesAsync(ct);
    }

    public async Task FailQaAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        if (caseData.CurrentStatus != CaseStatus.PlanQAInProgress)
        {
            throw new InvalidOperationException("Case must be in PlanQAInProgress status.");
        }

        var now = DateTimeOffset.UtcNow;
        var qaItem = await _dataAccess.GetOpenWorkItemAsync(caseId, WorkItemTypes.PlanQA, ct);
        if (qaItem is not null)
        {
            _workItemLifecycleService.RejectWorkItem(
                qaItem,
                completedBy: triggeredBy,
                resultCode: WorkItemResultCodes.Failed,
                remarks: reason,
                completedAtUtc: now);
        }

        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PlanQAFailed, new TransitionExecutionContext
        {
            TriggerName = "FailQa",
            TriggerType = WorkflowTriggerType.User,
            TriggeredBy = triggeredBy,
            ActorRoles = ["Physicist"],
            Reason = reason,
            Metadata = new { caseId, reason }
        }, ct);

        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PlanningInProgress, new TransitionExecutionContext
        {
            TriggerName = "ReturnToPlanningAfterQa",
            TriggerType = WorkflowTriggerType.System,
            TriggeredBy = "System",
            Reason = reason,
            Metadata = new { caseId, reason }
        }, ct);

        await _dataAccess.SaveChangesAsync(ct);
    }

    public async Task HandleSchedulingFailureAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        if (caseData.CurrentStatus != CaseStatus.SchedulingInProgress)
        {
            throw new InvalidOperationException("Case must be in SchedulingInProgress status.");
        }

        await _workItemLifecycleService.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
        {
            CaseId = caseId,
            Type = WorkItemTypes.ScheduleSync,
            AssignedRole = "Scheduler",
            PayloadJson = JsonSerializer.Serialize(new { reason }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, ct);

        await AddAuditAsync(caseId, "SchedulingFailure", caseData.CurrentStatus, caseData.CurrentStatus, new { reason, triggeredBy }, ct);
        await _dataAccess.AddCaseTransitionHistoryAsync(new CaseTransitionHistoryData
        {
            TransitionId = Guid.NewGuid(),
            CaseId = caseId,
            FromStatus = caseData.CurrentStatus.ToString(),
            ToStatus = caseData.CurrentStatus.ToString(),
            TriggerType = WorkflowTriggerType.User.ToString(),
            TriggerName = "SchedulingFailure",
            TriggeredBy = triggeredBy,
            Reason = reason,
            MetadataJson = JsonSerializer.Serialize(new { reason, triggeredBy }),
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        await _dataAccess.SaveChangesAsync(ct);
    }

    public async Task RetrySchedulingAsync(Guid caseId, string triggeredBy, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        if (caseData.CurrentStatus != CaseStatus.SchedulingInProgress)
        {
            throw new InvalidOperationException("Case must be in SchedulingInProgress status.");
        }

        await AddAuditAsync(caseId, "RetryScheduling", caseData.CurrentStatus, caseData.CurrentStatus, new { triggeredBy }, ct);
        await _dataAccess.SaveChangesAsync(ct);
    }

    public async Task PauseTreatmentAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.TreatmentPaused, new TransitionExecutionContext
        {
            TriggerName = "PauseTreatment",
            TriggerType = WorkflowTriggerType.User,
            TriggeredBy = triggeredBy,
            Reason = reason,
            Metadata = new { reason }
        }, ct);

        await EnsureTreatmentExceptionTaskAsync(caseId, reason, ct);
        await _dataAccess.SaveChangesAsync(ct);
    }

    public async Task InterruptTreatmentAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.TreatmentInterrupted, new TransitionExecutionContext
        {
            TriggerName = "InterruptTreatment",
            TriggerType = WorkflowTriggerType.ExternalEvent,
            TriggeredBy = triggeredBy,
            Reason = reason,
            Metadata = new { reason }
        }, ct);

        await EnsureTreatmentExceptionTaskAsync(caseId, reason, ct);
        await _dataAccess.SaveChangesAsync(ct);
    }

    public async Task ResumeTreatmentAsync(Guid caseId, string triggeredBy, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        if (caseData.CurrentStatus == CaseStatus.TreatmentPaused)
        {
            await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.Treating, new TransitionExecutionContext
            {
                TriggerName = "ResumeTreatment",
                TriggerType = WorkflowTriggerType.User,
                TriggeredBy = triggeredBy
            }, ct);
        }
        else if (caseData.CurrentStatus == CaseStatus.TreatmentInterrupted)
        {
            await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.Treating, new TransitionExecutionContext
            {
                TriggerName = "ResumeAfterInterruption",
                TriggerType = WorkflowTriggerType.User,
                TriggeredBy = triggeredBy
            }, ct);
        }
        else
        {
            throw new InvalidOperationException("Case must be in TreatmentPaused or TreatmentInterrupted status.");
        }

        await _dataAccess.SaveChangesAsync(ct);
    }

    public async Task CancelCaseAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.Cancelled, new TransitionExecutionContext
        {
            TriggerName = "CancelCase",
            TriggerType = WorkflowTriggerType.User,
            TriggeredBy = triggeredBy,
            Reason = reason,
            Metadata = new { reason }
        }, ct);

        var now = DateTimeOffset.UtcNow;
        var workItems = await _dataAccess.GetMutableWorkItemsByCaseIdAsync(caseId, ct);
        foreach (var item in workItems.Where(x => !IsWorkItemClosed(x.Status)))
        {
            _workItemLifecycleService.CancelWorkItem(
                item,
                completedBy: triggeredBy,
                resultCode: WorkItemResultCodes.Cancelled,
                remarks: reason,
                completedAtUtc: now);
        }

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
            Action = OutboxActions.SendToMonacoImport,
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

        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.ContoursUnderReview, new TransitionExecutionContext
        {
            TriggerName = "StartContourReview",
            TriggerType = WorkflowTriggerType.User,
            TriggeredBy = "User",
            ActorRoles = ["Physician", "Dosimetrist"]
        }, ct);

        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PlanningPending, new TransitionExecutionContext
        {
            TriggerName = "ApproveContours",
            TriggerType = WorkflowTriggerType.User,
            TriggeredBy = "User",
            ActorRoles = ["Physician", "Dosimetrist"]
        }, ct);

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

    private async Task EnsureTreatmentExceptionTaskAsync(Guid caseId, string reason, CancellationToken ct)
    {
        await _workItemLifecycleService.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
        {
            CaseId = caseId,
            Type = WorkItemTypes.TreatmentExceptionHandling,
            AssignedRole = "Physician",
            PayloadJson = JsonSerializer.Serialize(new { reason }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, ct);
    }

    private static bool IsWorkItemClosed(WorkItemStatus status)
    {
        return status is WorkItemStatus.Done
            or WorkItemStatus.Rejected
            or WorkItemStatus.Cancelled
            or WorkItemStatus.Skipped;
    }
}
