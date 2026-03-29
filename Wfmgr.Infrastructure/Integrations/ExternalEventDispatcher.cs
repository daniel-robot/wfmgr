using System.Text.Json;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Application.Integrations;
using Wfmgr.Application.Integrations.Dtos;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Dtos;
using Wfmgr.Application.Workflows.V1.StateMachine;
using Wfmgr.Application.Workflows.V1.WorkItems;
using Wfmgr.Domain.Enums;
using Wfmgr.Domain.Integrations;
using Wfmgr.Domain.WorkItems;

namespace Wfmgr.Infrastructure.Integrations;

public class ExternalEventDispatcher : IExternalEventDispatcher
{
    private readonly IWorkflowDataAccess _dataAccess;
    private readonly ICaseWorkflowService _workflowService;
    private readonly ICaseStateMachineService _stateMachineService;
    private readonly IWorkItemLifecycleService _workItemLifecycleService;

    public ExternalEventDispatcher(
        IWorkflowDataAccess dataAccess,
        ICaseWorkflowService workflowService,
        ICaseStateMachineService stateMachineService,
        IWorkItemLifecycleService workItemLifecycleService)
    {
        _dataAccess = dataAccess;
        _workflowService = workflowService;
        _stateMachineService = stateMachineService;
        _workItemLifecycleService = workItemLifecycleService;
    }

    public async Task DispatchAsync(ExternalIntegrationEventRequest request, CancellationToken ct)
    {
        if (await _dataAccess.ExternalEventExistsAsync(request.Source, request.Type, request.ExternalId, ct))
        {
            return;
        }

        var resolvedCase = await ResolveCaseAsync(request, ct);
        var caseId = resolvedCase?.CaseId;

        try
        {
            await HandleEventAsync(request, resolvedCase, ct);

            if (caseId is not null && !string.IsNullOrWhiteSpace(request.ExternalEntityType) && !string.IsNullOrWhiteSpace(request.ExternalEntityId))
            {
                await _dataAccess.UpsertIntegrationReferenceAsync(
                    caseId.Value,
                    request.Source,
                    request.ExternalEntityType,
                    request.ExternalEntityId,
                    request.ExternalStatus,
                    request.MetadataJson,
                    ct);
            }

            await _dataAccess.AddExternalEventAsync(new ExternalEventData
            {
                EventId = Guid.NewGuid(),
                Source = request.Source,
                Type = request.Type,
                ExternalId = request.ExternalId,
                CaseCorrelationKey = request.CaseAccessionNumber,
                CaseId = caseId,
                PayloadJson = request.PayloadJson ?? JsonSerializer.Serialize(request),
                ReceivedAt = request.OccurredAt,
                ProcessedAt = DateTimeOffset.UtcNow,
                ProcessStatus = "Processed"
            }, ct);
        }
        catch (Exception ex)
        {
            await _dataAccess.AddExternalEventAsync(new ExternalEventData
            {
                EventId = Guid.NewGuid(),
                Source = request.Source,
                Type = request.Type,
                ExternalId = request.ExternalId,
                CaseCorrelationKey = request.CaseAccessionNumber,
                CaseId = caseId,
                PayloadJson = request.PayloadJson ?? JsonSerializer.Serialize(request),
                ReceivedAt = request.OccurredAt,
                ProcessedAt = DateTimeOffset.UtcNow,
                ProcessStatus = "Failed",
                Error = ex.Message
            }, ct);

            throw;
        }

        await _dataAccess.SaveChangesAsync(ct);
    }

    private async Task HandleEventAsync(ExternalIntegrationEventRequest request, CaseData? caseData, CancellationToken ct)
    {
        switch (request.Type)
        {
            case ExternalIntegrationEventTypes.CtImageStored:
                await HandleCtImageStoredAsync(request, ct);
                break;

            case ExternalIntegrationEventTypes.CtImageStorageFailed:
                await EnsureCaseAsync(caseData, request.Type);
                await _workItemLifecycleService.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
                {
                    CaseId = caseData!.CaseId,
                    Type = WorkItemTypes.ImageValidation,
                    AssignedRole = "SimTech",
                    PayloadJson = JsonSerializer.Serialize(new { request.FailureReason, request.Type }),
                    CreatedAtUtc = DateTimeOffset.UtcNow
                }, ct);
                break;

            case ExternalIntegrationEventTypes.AutoContourStarted:
            case ExternalIntegrationEventTypes.AutoContourProgress:
                await EnsureCaseAsync(caseData, request.Type);
                await _dataAccess.EnqueueOutboxAsync(caseData!.CaseId, "PvMed", OutboxActions.QueryContourStatus, request.PayloadJson ?? "{}", ct);
                break;

            case ExternalIntegrationEventTypes.AutoContourCompleted:
            case ExternalIntegrationEventTypes.ManualContourCompleted:
                await HandlePvMedEventAsync(request, "PVMED_AUTOCONTOUR_COMPLETED", ct);
                break;

            case ExternalIntegrationEventTypes.AutoContourFailed:
                await HandlePvMedEventAsync(request, "PVMED_AUTOCONTOUR_FAILED", ct);
                break;

            case ExternalIntegrationEventTypes.MonacoImportAccepted:
                await EnsureCaseAsync(caseData, request.Type);
                if (caseData!.CurrentStatus == CaseStatus.PlanningPending)
                {
                    await _stateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PlanningAssigned, new TransitionExecutionContext
                    {
                        TriggerName = "AssignPlanning",
                        TriggerType = WorkflowTriggerType.System,
                        TriggeredBy = "Monaco",
                        ActorRoles = ["Dosimetrist"],
                        Metadata = request
                    }, ct);
                }
                break;

            case ExternalIntegrationEventTypes.MonacoImportFailed:
                await EnsureCaseAsync(caseData, request.Type);
                await _workItemLifecycleService.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
                {
                    CaseId = caseData!.CaseId,
                    Type = WorkItemTypes.PlanAssignment,
                    AssignedRole = "Dosimetrist",
                    PayloadJson = JsonSerializer.Serialize(new { request.FailureReason, request.Type }),
                    CreatedAtUtc = DateTimeOffset.UtcNow
                }, ct);
                break;

            case ExternalIntegrationEventTypes.PlanCreated:
                await EnsureCaseAsync(caseData, request.Type);
                if (int.TryParse(request.PlanVersionNo, out var createdVersion))
                {
                    caseData!.CurrentPlanVersionNo = createdVersion;
                }
                if (caseData!.CurrentStatus == CaseStatus.PlanningAssigned)
                {
                    await _stateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PlanningInProgress, new TransitionExecutionContext
                    {
                        TriggerName = "StartPlanning",
                        TriggerType = WorkflowTriggerType.System,
                        TriggeredBy = "Monaco",
                        ActorRoles = ["Dosimetrist"],
                        Metadata = request
                    }, ct);
                }
                break;

            case ExternalIntegrationEventTypes.PlanUpdated:
                await EnsureCaseAsync(caseData, request.Type);
                if (int.TryParse(request.PlanVersionNo, out var updatedVersion))
                {
                    caseData!.CurrentPlanVersionNo = Math.Max(caseData.CurrentPlanVersionNo ?? 0, updatedVersion);
                }
                break;

            case ExternalIntegrationEventTypes.PlanReviewCompleted:
                await EnsureCaseAsync(caseData, request.Type);
                if (caseData!.CurrentStatus == CaseStatus.PlanUnderReview)
                {
                    await _stateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PlanReviewed, new TransitionExecutionContext
                    {
                        TriggerName = "ApprovePlan",
                        TriggerType = WorkflowTriggerType.ExternalEvent,
                        TriggeredBy = "Monaco",
                        ActorRoles = ["Physician"],
                        Metadata = request
                    }, ct);
                }

                await _dataAccess.EnqueueOutboxAsync(caseData.CaseId, "MSQ", OutboxActions.GeneratePrescription, request.PayloadJson ?? "{}", ct);
                break;

            case ExternalIntegrationEventTypes.PlanReviewFailed:
                await EnsureCaseAsync(caseData, request.Type);
                await _workflowService.RejectPlanReviewAsync(caseData!.CaseId, request.FailureReason ?? "Monaco review failed", request.Source, ct);
                break;

            case ExternalIntegrationEventTypes.PrescriptionGenerated:
                await EnsureCaseAsync(caseData, request.Type);
                if (caseData!.CurrentStatus == CaseStatus.PrescriptionGenerating)
                {
                    await _stateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PrescriptionReady, new TransitionExecutionContext
                    {
                        TriggerName = "PrescriptionReady",
                        TriggerType = WorkflowTriggerType.ExternalEvent,
                        TriggeredBy = "MSQ",
                        Metadata = request
                    }, ct);
                }

                await _dataAccess.EnqueueOutboxAsync(caseData.CaseId, "MSQ", OutboxActions.SyncSchedule, request.PayloadJson ?? "{}", ct);
                break;

            case ExternalIntegrationEventTypes.PrescriptionSyncFailed:
                await EnsureCaseAsync(caseData, request.Type);
                await _workflowService.HandlePrescriptionSyncFailureAsync(caseData!.CaseId, request.FailureReason ?? "MSQ prescription sync failed", request.Source, ct);
                break;

            case ExternalIntegrationEventTypes.ScheduleSynced:
                await EnsureCaseAsync(caseData, request.Type);
                if (caseData!.CurrentStatus == CaseStatus.SchedulingInProgress)
                {
                    await _stateMachineService.ApplyTransitionAsync(caseData, CaseStatus.Scheduled, new TransitionExecutionContext
                    {
                        TriggerName = "CompleteScheduling",
                        TriggerType = WorkflowTriggerType.ExternalEvent,
                        TriggeredBy = "MSQ",
                        ActorRoles = ["Scheduler"],
                        Metadata = request
                    }, ct);
                }
                break;

            case ExternalIntegrationEventTypes.TreatmentStarted:
                await EnsureCaseAsync(caseData, request.Type);
                if (caseData!.CurrentStatus == CaseStatus.QueuePending)
                {
                    await _stateMachineService.ApplyTransitionAsync(caseData, CaseStatus.Treating, new TransitionExecutionContext
                    {
                        TriggerName = "StartTreatment",
                        TriggerType = WorkflowTriggerType.ExternalEvent,
                        TriggeredBy = "MSQ",
                        ActorRoles = ["Therapist"],
                        Metadata = request
                    }, ct);
                }

                await _dataAccess.EnqueueOutboxAsync(caseData.CaseId, "MSQ", OutboxActions.QueryTreatmentProgress, request.PayloadJson ?? "{}", ct);
                break;

            case ExternalIntegrationEventTypes.TreatmentFractionCompleted:
                await EnsureCaseAsync(caseData, request.Type);
                break;

            case ExternalIntegrationEventTypes.TreatmentCourseCompleted:
                await EnsureCaseAsync(caseData, request.Type);
                if (caseData!.CurrentStatus == CaseStatus.Treating)
                {
                    await _stateMachineService.ApplyTransitionAsync(caseData, CaseStatus.TreatmentCompleted, new TransitionExecutionContext
                    {
                        TriggerName = "CompleteTreatment",
                        TriggerType = WorkflowTriggerType.ExternalEvent,
                        TriggeredBy = "MSQ",
                        Metadata = request
                    }, ct);
                }
                break;

            case ExternalIntegrationEventTypes.TreatmentInterrupted:
                await EnsureCaseAsync(caseData, request.Type);
                await _workflowService.InterruptTreatmentAsync(caseData!.CaseId, request.FailureReason ?? "External interruption", request.Source, ct);
                break;

            default:
                throw new InvalidOperationException($"Unsupported external event type '{request.Type}'.");
        }
    }

    private async Task HandleCtImageStoredAsync(ExternalIntegrationEventRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CaseAccessionNumber))
        {
            throw new InvalidOperationException("CaseAccessionNumber is required for CT_IMAGE_STORED events.");
        }

        if (string.IsNullOrWhiteSpace(request.CtStudyInstanceUid) || string.IsNullOrWhiteSpace(request.CtWadoRsUrl))
        {
            throw new InvalidOperationException("CtStudyInstanceUid and CtWadoRsUrl are required for CT_IMAGE_STORED events.");
        }

        await _workflowService.HandleCtImageStoredAsync(new CtImageStoredRequest
        {
            ExternalEventId = request.ExternalId,
            AccessionNumber = request.CaseAccessionNumber,
            DicomRef = new DicomRef { StudyInstanceUid = request.CtStudyInstanceUid },
            DicomWebLocation = new DicomWebLocation { WadoRsUrl = request.CtWadoRsUrl },
            OccurredAt = request.OccurredAt
        }, ct);
    }

    private async Task HandlePvMedEventAsync(ExternalIntegrationEventRequest request, string mappedType, CancellationToken ct)
    {
        var caseData = await ResolveCaseAsync(request, ct);
        await EnsureCaseAsync(caseData, mappedType);

        await _workflowService.HandlePvMedEventAsync(new PvMedEventRequest
        {
            ExternalEventId = request.ExternalId,
            CaseId = caseData!.CaseId,
            Type = mappedType,
            PvMedJob = new PvMedJob
            {
                JobId = request.ExternalEntityId ?? request.ExternalId
            },
            PvMedResult = string.IsNullOrWhiteSpace(request.RtStructSeriesInstanceUid)
                ? null
                : new PvMedResult
                {
                    RtStructLocation = new RtStructLocation
                    {
                        SeriesInstanceUid = request.RtStructSeriesInstanceUid
                    }
                },
            OccurredAt = request.OccurredAt
        }, ct);
    }

    private async Task<CaseData?> ResolveCaseAsync(ExternalIntegrationEventRequest request, CancellationToken ct)
    {
        if (request.CaseId is not null)
        {
            return await _dataAccess.GetCaseByIdAsync(request.CaseId.Value, ct);
        }

        if (!string.IsNullOrWhiteSpace(request.CaseAccessionNumber))
        {
            return await _dataAccess.GetCaseByAccessionNumberAsync(request.CaseAccessionNumber, ct);
        }

        return null;
    }

    private static Task EnsureCaseAsync(CaseData? caseData, string eventType)
    {
        if (caseData is null)
        {
            throw new InvalidOperationException($"Unable to resolve case for event '{eventType}'.");
        }

        return Task.CompletedTask;
    }
}
