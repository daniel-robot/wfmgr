using System.Text.Json;
using Microsoft.Extensions.Logging;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Application.EngineAdapters;
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Application.Workflows.V1.Gates;
using Wfmgr.Application.Workflows.V1.Outbox;
using Wfmgr.Application.Workflows.V1.WorkItems;
using Wfmgr.Domain.Enums;
using Wfmgr.Domain.Integrations;
using EngineAbstractions = Wfmgr.Engine.Abstractions;

namespace Wfmgr.Application.Workflows.V1.Compensation;

/// <summary>
/// Default implementation of <see cref="IWorkflowCompensationService"/>.
/// </summary>
public sealed class WorkflowCompensationService : IWorkflowCompensationService
{
    private readonly IWorkflowDataAccess _dataAccess;
    private readonly EngineAbstractions.ITransitionEngine _engine;
    private readonly IWorkItemLifecycleService _workItems;
    private readonly IOutboxRouteProvider _routeProvider;
    private readonly ILogger<WorkflowCompensationService> _logger;

    public WorkflowCompensationService(
        IWorkflowDataAccess dataAccess,
        EngineAbstractions.ITransitionEngine engine,
        IWorkItemLifecycleService workItems,
        IOutboxRouteProvider routeProvider,
        ILogger<WorkflowCompensationService> logger)
    {
        _dataAccess = dataAccess;
        _engine = engine;
        _workItems = workItems;
        _routeProvider = routeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CompensationResult> HandleFailureAsync(
        Guid caseId,
        string failedStepCode,
        CompensationContext context,
        CancellationToken ct = default)
    {
        // ── 1. Resolve compensation definition ─────────────────────────────────
        if (!WorkflowCompensationCatalog.ByFailedStep.TryGetValue(failedStepCode, out var definitions)
            || definitions.Count == 0)
        {
            _logger.LogWarning(
                "No compensation definition for failed step '{Step}'", failedStepCode);
            return CompensationResult.Failed(
                CompensationFailureReason.DefinitionNotFound,
                $"No compensation definition found for failed step '{failedStepCode}'.");
        }

        // Use the first matching definition (catalog has at most one per step code).
        var definition = definitions[0];

        // ── 2. Load case ───────────────────────────────────────────────────────
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct);
        if (caseData is null)
        {
            return CompensationResult.Failed(
                CompensationFailureReason.CaseNotFound,
                $"Case '{caseId}' not found.");
        }

        var previousStatus = caseData.CurrentStatus;
        var now = DateTimeOffset.UtcNow;

        // ── 3. Apply status change via engine (when needed) ────────────────────
        CaseStatus? newStatus = null;
        if (definition.TargetStatus.HasValue && definition.TargetStatus.Value != previousStatus)
        {
            var triggerName = $"Compensate:{definition.Code}";
            var engineSubject = new CaseWorkflowSubject(caseData);
            var gateCtx = new Wfmgr.Engine.Core.GateValidationContext
            {
                UserId = context.UserId,
                Roles = [],
                Reason = context.Reason,
                ExternalEventPayload = context.ExternalEventPayload,
                Metadata = context.Metadata,
            };

            var transitionResult = await _engine.ApplyTransitionAsync(
                engineSubject,
                triggerName,
                gateCtx,
                ct,
                fallbackToStatus: definition.TargetStatus.Value.ToString());

            if (!transitionResult.IsSuccess)
            {
                // Transition service returned an unexpected hard error — surface it.
                return CompensationResult.Failed(
                    CompensationFailureReason.WorkItemCreationFailed,
                    $"Status transition to {definition.TargetStatus.Value} failed: " +
                    transitionResult.ToSummary());
            }

            newStatus = definition.TargetStatus.Value;
        }
        else
        {
            // No status change: still write audit + history directly.
            await WriteAuditAsync(caseData.CaseId, definition, context, previousStatus, null, now, ct);
        }

        // ── 4. Create work item (idempotency-guarded) ──────────────────────────
        string? workItemCreated = null;
        if (!string.IsNullOrWhiteSpace(definition.WorkItemToCreate))
        {
            var existing = await _dataAccess.GetOpenWorkItemAsync(
                caseData.CaseId, definition.WorkItemToCreate, ct);

            if (existing is null)
            {
                var role = WorkItemDefaultRoles.GetValueOrDefault(
                    definition.WorkItemToCreate, "System");

                await _workItems.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
                {
                    CaseId = caseData.CaseId,
                    Type = definition.WorkItemToCreate,
                    AssignedRole = role,
                    PayloadJson = JsonSerializer.Serialize(new
                    {
                        compensationCode = definition.Code,
                        failedStepCode,
                        reason = context.Reason,
                        sourceSystem = context.SourceSystem,
                        retryCount = context.RetryCount,
                    }),
                    CreatedAtUtc = now,
                }, ct);

                workItemCreated = definition.WorkItemToCreate;
            }
        }

        // ── 5. Dispatch outbox retry (when policy allows and budget remains) ───
        var retryDispatched = false;
        if (ShouldRetry(definition.RetryPolicy, context.RetryCount))
        {
            var nextRetry = ComputeNextRetry(definition.RetryPolicy!, context.RetryCount);
            var retryPayload = JsonSerializer.Serialize(new
            {
                caseId,
                compensationCode = definition.Code,
                failedStepCode,
                retryAttempt = context.RetryCount + 1,
                reason = context.Reason,
                sourceSystem = context.SourceSystem,
                failedOutboxMessageId = context.FailedOutboxMessageId,
                nextRetryAt = nextRetry,
                metadata = context.Metadata,
            });

            await _dataAccess.AddOutboxMessageAsync(BuildRetryOutboxMessage(
                caseId, definition, context, _routeProvider, retryPayload, nextRetry, now), ct);

            retryDispatched = true;
            _logger.LogInformation(
                "Compensation {Code}: retry outbox enqueued for case {CaseId} (attempt {Attempt}/{Max}, nextRetryAt: {NextRetry})",
                definition.Code, caseId, context.RetryCount + 1, definition.RetryPolicy!.MaxAttempts, nextRetry);
        }

        _logger.LogInformation(
            "Compensation {Code} applied for case {CaseId}: {From} \u2192 {To}, workItem: {WorkItem}, retryDispatched: {Retry}",
            definition.Code, caseId, previousStatus, newStatus?.ToString() ?? "(unchanged)",
            workItemCreated ?? "none", retryDispatched);

        return CompensationResult.Succeeded(
            definition.Code,
            previousStatus,
            newStatus,
            workItemCreated,
            retryDispatched);

        // (SaveChangesAsync is intentionally NOT called here — the caller owns the unit of work.)
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task WriteAuditAsync(
        Guid caseId,
        CompensationDefinition definition,
        CompensationContext context,
        CaseStatus fromStatus,
        CaseStatus? toStatus,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await _dataAccess.AddAuditLogAsync(new AuditLogData
        {
            AuditId = Guid.NewGuid(),
            CaseId = caseId,
            ActorType = string.IsNullOrWhiteSpace(context.UserId) ? "System" : "User",
            ActorId = context.UserId,
            Action = $"Compensation:{definition.Code}",
            FromStatus = fromStatus,
            ToStatus = toStatus,
            SnapshotJson = JsonSerializer.Serialize(new
            {
                compensationCode = definition.Code,
                failedStepCode = definition.FailedStepCode,
                failureCondition = definition.FailureCondition,
                compensationAction = definition.CompensationAction,
                reason = context.Reason,
                sourceSystem = context.SourceSystem,
                retryCount = context.RetryCount,
                failedOutboxMessageId = context.FailedOutboxMessageId,
                manualInterventionRequired = definition.ManualInterventionRequired,
                metadata = context.Metadata,
            }),
            CreatedAt = now,
        }, ct);
    }

    private static bool ShouldRetry(RetryPolicy? policy, int currentRetryCount)
    {
        if (policy is null)
            return false;

        // MaxAttempts == 0 is used for "unlimited polling" (CMP-016); never auto-enqueue.
        if (policy.MaxAttempts == 0)
            return false;

        return currentRetryCount < policy.MaxAttempts;
    }

    private static DateTimeOffset ComputeNextRetry(RetryPolicy policy, int currentRetryCount)
    {
        var delay = policy.InitialDelay ?? TimeSpan.FromSeconds(30);
        return policy.Strategy switch
        {
            "ExponentialBackoff" =>
                DateTimeOffset.UtcNow + TimeSpan.FromTicks(
                    (long)(delay.Ticks * Math.Pow(2, currentRetryCount))),
            _ =>
                DateTimeOffset.UtcNow + delay,
        };
    }

    private static OutboxMessageData BuildRetryOutboxMessage(
        Guid caseId,
        CompensationDefinition definition,
        CompensationContext context,
        IOutboxRouteProvider routeProvider,
        string payloadJson,
        DateTimeOffset nextRetryAt,
        DateTimeOffset now)
    {
        var route = routeProvider.GetRouteByStepCode(definition.FailedStepCode);
        var targetSystem = route?.TargetSystem ?? context.SourceSystem ?? "System";
        var action = route?.Action ?? OutboxActions.QueryContourStatus;

        return new OutboxMessageData
        {
            MessageId = Guid.NewGuid(),
            CaseId = caseId,
            TargetSystem = targetSystem,
            Action = action,
            PayloadJson = payloadJson,
            Status = OutboxStatus.New,
            RetryCount = context.RetryCount,
            NextRetryAt = nextRetryAt,
            CreatedAt = now,
        };
    }

    // ── Static role defaults ──────────────────────────────────────────────────

    private static readonly IReadOnlyDictionary<string, string> WorkItemDefaultRoles =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ImageForwardToContourTool"]  = "System",
            ["ManualContouring"]           = "Physician/ThirdPartyOperator",
            ["ContourRework"]              = "Physician/ThirdPartyOperator",
            ["PlanDesign"]                 = "Dosimetrist",
            ["PrescriptionSync"]           = "Physicist/System",
            ["PlanQA"]                     = "Physicist/QAReviewer",
            ["ScheduleSync"]               = "Scheduler/System",
            ["TreatmentOrder"]             = "Physician",
            ["QueueCall"]                  = "System",
            ["TreatmentMonitor"]           = "System",
            ["TreatmentExceptionHandling"] = "Admin",
            ["PostTreatmentReview"]        = "Physician",
            ["ArchiveReview"]              = "System/Admin",
        };
}
