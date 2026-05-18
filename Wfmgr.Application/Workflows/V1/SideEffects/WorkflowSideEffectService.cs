using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Application.Workflows.V1.Outbox;
using Wfmgr.Application.Workflows.V1.WorkItems;
using Wfmgr.Domain.Integrations;
using Wfmgr.Domain.WorkItems;

namespace Wfmgr.Application.Workflows.V1.SideEffects;

/// <summary>
/// Default implementation of <see cref="IWorkflowSideEffectService"/>.
/// </summary>
public sealed class WorkflowSideEffectService : IWorkflowSideEffectService
{
    private readonly IWorkflowDataAccess _dataAccess;
    private readonly IWorkItemLifecycleService _workItems;
    private readonly IWorkflowProfileResolver _profileResolver;
    private readonly IOutboxRoutingPolicy _routing;
    private readonly ILogger<WorkflowSideEffectService> _logger;

    public WorkflowSideEffectService(
        IWorkflowDataAccess dataAccess,
        IWorkItemLifecycleService workItems,
        IWorkflowProfileResolver profileResolver,
        IOutboxRoutingPolicy routing,
        ILogger<WorkflowSideEffectService> logger)
    {
        _dataAccess = dataAccess;
        _workItems = workItems;
        _profileResolver = profileResolver;
        _routing = routing;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(
        TransitionDefinition definition,
        SideEffectContext context,
        CancellationToken ct = default)
    {
        await CreateWorkItemsAsync(definition, context, ct);
        await DispatchOutboxAsync(definition, context, ct);
    }

    // ── Work item creation ────────────────────────────────────────────────────

    private async Task CreateWorkItemsAsync(
        TransitionDefinition definition,
        SideEffectContext context,
        CancellationToken ct)
    {
        // Resolve profile policies lazily (at most once per slot per call).
        S1ContouringStrategy? s1 = null;
        S2ContourReviewPolicy? s2 = null;
        S3PlanDispatchPolicy? s3 = null;
        S4PlanReReviewPolicy? s4 = null;
        S5PlanDoubleCheckPolicy? s5 = null;

        var caseData = context.CaseData;

        foreach (var type in definition.WorkItemsToCreate)
        {
            if (!DefaultWorkItemRoles.TryGetValue(type, out var defaultRole))
                continue; // unknown type — not managed by this service

            // Idempotency: if an open work item of this type already exists, skip.
            var existing = await _dataAccess.GetOpenWorkItemAsync(caseData.CaseId, type, ct);
            if (existing is not null)
                continue;

            var role = type switch
            {
                WorkItemTypes.ManualContouring or
                WorkItemTypes.ImageForwardToContourTool =>
                    (s1 ??= await _profileResolver.ResolveS1ContouringStrategyAsync(
                            caseData.HospitalId, caseData.SiteId, caseData.DepartmentId, ct))
                        .Fallback.ManualWorkItemRole.NullIfWhiteSpace() ?? defaultRole,

                WorkItemTypes.ContourRework =>
                    (s2 ??= await _profileResolver.ResolveS2ContourReviewPolicyAsync(
                            caseData.HospitalId, caseData.SiteId, caseData.DepartmentId, ct))
                        .OnReject.ReworkWorkItemRole.NullIfWhiteSpace() ?? defaultRole,

                WorkItemTypes.PlanAssignment or
                WorkItemTypes.PlanDesign =>
                    (s3 ??= await _profileResolver.ResolveS3PlanDispatchPolicyAsync(
                            caseData.HospitalId, caseData.SiteId, caseData.DepartmentId, ct))
                        .TargetRole.NullIfWhiteSpace() ?? defaultRole,

                WorkItemTypes.PlanReReview =>
                    (s4 ??= await _profileResolver.ResolveS4PlanReReviewPolicyAsync(
                            caseData.HospitalId, caseData.SiteId, caseData.DepartmentId, ct))
                        .ReviewRole.NullIfWhiteSpace() ?? defaultRole,

                WorkItemTypes.PlanDoubleCheck =>
                    (s5 ??= await _profileResolver.ResolveS5PlanDoubleCheckPolicyAsync(
                            caseData.HospitalId, caseData.SiteId, caseData.DepartmentId, ct))
                        .WorkItemRole.NullIfWhiteSpace() ?? defaultRole,

                _ => defaultRole,
            };

            await _workItems.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
            {
                CaseId = caseData.CaseId,
                Type = type,
                AssignedRole = role,
                CreatedAtUtc = context.Now,
            }, ct);

            _logger.LogDebug(
                "Side effect: created work item '{Type}' (role: {Role}) for case {CaseId} after transition {Code}",
                type, role, caseData.CaseId, definition.Code);
        }
    }

    // ── Outbox dispatch ───────────────────────────────────────────────────────

    private async Task DispatchOutboxAsync(
        TransitionDefinition definition,
        SideEffectContext context,
        CancellationToken ct)
    {
        var caseData = context.CaseData;
        S1ContouringStrategy? s1 = null;

        foreach (var action in definition.SuccessActions)
        {
            if (!OutboxActionMap.TryGetValue(action, out var descriptor))
                continue;

            // For contouring actions, resolve the actual provider from S1 configuration.
            var targetSystem = descriptor.TargetSystem;
            if (descriptor.Action == OutboxActions.SendImagesToContourTool)
            {
                s1 ??= await _profileResolver.ResolveS1ContouringStrategyAsync(
                    caseData.HospitalId, caseData.SiteId, caseData.DepartmentId, ct);
                targetSystem = s1.Provider.NullIfWhiteSpace() ?? "PvMed";
            }

            var payload = JsonSerializer.Serialize(new
            {
                caseId = caseData.CaseId,
                accessionNumber = caseData.AccessionNumber,
                triggerName = definition.TriggerName,
                transitionCode = definition.Code,
                triggeredBy = context.ValidationContext.UserId,
                reason = context.ValidationContext.Reason,
            });

            await _dataAccess.EnqueueOutboxAsync(
                caseId: caseData.CaseId,
                targetSystem: targetSystem,
                action: descriptor.Action,
                payloadJson: payload,
                messageType: descriptor.MessageType,
                schemaVersion: 1,
                correlationId: caseData.CaseId,
                traceparent: Wfmgr.Application.Diagnostics.WfmgrActivitySource.CurrentTraceparent(),
                deliveryMode: _routing.GetDeliveryMode(descriptor.Action),
                ct: ct);

            _logger.LogDebug(
                "Side effect: enqueued outbox '{Action}' to '{System}' for case {CaseId} after transition {Code}",
                descriptor.Action, targetSystem, caseData.CaseId, definition.Code);
        }
    }

    // ── Static maps ───────────────────────────────────────────────────────────

    private sealed record OutboxDescriptor(string TargetSystem, string Action, string MessageType);

    /// <summary>
    /// Maps <see cref="TransitionDefinition.SuccessActions"/> string identifiers to outbox
    /// integration descriptors.  Only actions that correspond to an external integration message
    /// are listed; all others are silently ignored.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, OutboxDescriptor> OutboxActionMap =
        new ReadOnlyDictionary<string, OutboxDescriptor>(
            new Dictionary<string, OutboxDescriptor>(StringComparer.OrdinalIgnoreCase)
            {
                // Contouring tool — target system resolved at runtime from S1 profile.
                ["CreateOutboxSendImagesToContourTool"] = new("PvMed",   OutboxActions.SendImagesToContourTool,  typeof(Wfmgr.Contracts.Contouring.SendImagesToContourTool.V1).FullName!),
                ["CreateOutboxRestartContouring"]       = new("PvMed",   OutboxActions.SendImagesToContourTool,  typeof(Wfmgr.Contracts.Contouring.SendImagesToContourTool.V1).FullName!),

                // Monaco import.
                ["SendToMonacoImport"]                  = new("Monaco",  OutboxActions.SendToMonacoImport,       typeof(Wfmgr.Contracts.Monaco.SendToMonacoImport.V1).FullName!),

                // Prescription generation / retry.
                ["CreateOutboxGeneratePrescription"]    = new("PvMed",   OutboxActions.GeneratePrescription,     typeof(Wfmgr.Contracts.Prescription.GeneratePrescription.V1).FullName!),
                ["CreateOutboxPrescriptionSync"]        = new("PvMed",   OutboxActions.GeneratePrescription,     typeof(Wfmgr.Contracts.Prescription.GeneratePrescription.V1).FullName!),

                // Schedule synchronisation.
                ["StartScheduleWatch"]                  = new("MSQ",     OutboxActions.SyncSchedule,             typeof(Wfmgr.Contracts.Scheduling.SyncSchedule.V1).FullName!),

                // Treatment progress polling.
                ["CreateTreatmentMonitor"]              = new("Monaco",  OutboxActions.QueryTreatmentProgress,   typeof(Wfmgr.Contracts.Monaco.QueryTreatmentProgress.V1).FullName!),
                ["UpdateProgress"]                      = new("Monaco",  OutboxActions.QueryTreatmentProgress,   typeof(Wfmgr.Contracts.Monaco.QueryTreatmentProgress.V1).FullName!),
            });

    /// <summary>
    /// Default assigned roles for each work item type.  Profile-driven types may have their
    /// roles overridden at runtime by consulting the relevant workflow profile slot.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> DefaultWorkItemRoles =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [WorkItemTypes.SimulationSchedule]         = "SimTech/Scheduler",
                [WorkItemTypes.SimulationRecord]           = "SimTech",
                [WorkItemTypes.ImageValidation]            = "SimTech/Physicist",
                [WorkItemTypes.ImageForwardToContourTool]  = "System",
                [WorkItemTypes.AutoContourMonitor]         = "System",
                [WorkItemTypes.ManualContouring]           = "Physician/ThirdPartyOperator",
                [WorkItemTypes.ContourReview]              = "Physician",
                [WorkItemTypes.ContourSecondReview]        = "Physician/Physicist",
                [WorkItemTypes.ContourRework]              = "Physician/ThirdPartyOperator",
                [WorkItemTypes.PlanAssignment]             = "Scheduler/System",
                [WorkItemTypes.PlanDesign]                 = "Dosimetrist",
                [WorkItemTypes.PlanEvaluation]             = "Physicist/Physician",
                [WorkItemTypes.PlanReReview]               = "Physician/Physicist",
                [WorkItemTypes.PrescriptionSync]           = "Physicist/System",
                [WorkItemTypes.PlanQA]                     = "Physicist/QAReviewer",
                [WorkItemTypes.PlanDoubleCheck]            = "Physicist",
                [WorkItemTypes.ScheduleSync]               = "Scheduler/System",
                [WorkItemTypes.TreatmentOrder]             = "Physician",
                [WorkItemTypes.QueueCall]                  = "System",
                [WorkItemTypes.TreatmentMonitor]           = "System",
                [WorkItemTypes.TreatmentExceptionHandling] = "Admin",
                [WorkItemTypes.PostTreatmentReview]        = "Physician",
                [WorkItemTypes.ArchiveReview]              = "System/Admin",
            });
}

file static class StringExtensions
{
    internal static string? NullIfWhiteSpace(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
