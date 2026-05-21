
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Application.Diagnostics;
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Application.Workflows.V1.Gates;
using Wfmgr.Application.Workflows.V1.SideEffects;
using Wfmgr.Domain.Enums;
using Wfmgr.Application.Workflows.V1.StateMachine;

namespace Wfmgr.Application.Workflows.V1;

/// <summary>
/// Central implementation of <see cref="ICaseTransitionService"/>.
/// <para>
/// Execution pipeline per call:
/// 1. Catalog lookup by <c>triggerName</c> + <c>fromStatus</c>.
/// 2. <c>RequiredRoles</c> check.
/// 3. <see cref="IGateValidationService.ValidateAsync"/> for all declared <c>GateChecks</c>.
/// 4. Mutate <c>CaseData.CurrentStatus</c> + increment <c>StatusVersion</c>.
/// 5. Write <c>AuditLog</c>.
/// 6. Write <c>CaseTransitionHistory</c>.
/// 7. <see cref="IWorkflowSideEffectService.ExecuteAsync"/> (catalog-matched transitions only).
/// </para>
/// <para>
/// When <c>triggerName</c> has no catalog entry and <c>fallbackToStatus</c> is supplied,
/// steps 2–3 and 7 are skipped — backward-compatible bridge for legacy trigger names.
/// </para>
/// </summary>

public sealed class CaseTransitionService : ICaseTransitionService
{


    public async Task<TransitionExecutionResult> ApplyTransitionAsync(
        IWorkflowSubject subject,
        string triggerName,
        GateValidationContext context,
        CancellationToken ct = default,
        CaseStatus? fallbackToStatus = null)
    {
        // For now, only CaseData is supported
        if (subject is not CaseData caseData)
            throw new NotSupportedException($"Only CaseData subjects are supported (got {subject.GetType().Name})");
        return await ApplyTransitionAsync(caseData, triggerName, context, ct, fallbackToStatus);
    }
    private readonly IWorkflowDataAccess _dataAccess;
    private readonly IGateValidationService _gateValidation;
    private readonly IWorkflowSideEffectService _sideEffects;
    private readonly IWorkflowTransitionCatalogService _catalog;
    private readonly ILogger<CaseTransitionService> _logger;

    public CaseTransitionService(
        IWorkflowDataAccess dataAccess,
        IGateValidationService gateValidation,
        IWorkflowSideEffectService sideEffects,
        IWorkflowTransitionCatalogService catalog,
        ILogger<CaseTransitionService> logger)
    {
        _dataAccess = dataAccess;
        _gateValidation = gateValidation;
        _sideEffects = sideEffects;
        _catalog = catalog;
        _logger = logger;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<TransitionExecutionResult> ApplyTransitionAsync(
        Guid caseId,
        string triggerName,
        GateValidationContext context,
        CancellationToken ct = default,
        CaseStatus? fallbackToStatus = null)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(caseId, ct)
            ?? throw new InvalidOperationException($"Case '{caseId}' not found.");

        return await ApplyTransitionAsync(caseData, triggerName, context, ct, fallbackToStatus);
    }

    /// <inheritdoc/>
    public async Task<TransitionExecutionResult> ApplyTransitionAsync(
        CaseData caseData,
        string triggerName,
        GateValidationContext context,
        CancellationToken ct = default,
        CaseStatus? fallbackToStatus = null)
    {using var activity = WfmgrActivitySource.Source.StartActivity(WfmgrActivitySource.ApplyTransition);
        activity?.SetTag(WfmgrActivitySource.TagCaseId, caseData.CaseId);
        activity?.SetTag(WfmgrActivitySource.TagTriggerName, triggerName);
        activity?.SetTag(WfmgrActivitySource.TagFromStatus, caseData.CurrentStatus.ToString());

        
        var fromStatus = caseData.CurrentStatus;

        // ── 1. Look up transition definition in catalog ────────────────────────
        var definition = await _catalog.FindByTriggerAsync(triggerName, fromStatus.ToString(), ct);

        CaseStatus toStatus;
        string? transitionCode = null;
        bool catalogMatched = definition is not null;

        if (definition is not null)
        {
            transitionCode = definition.Code;
            toStatus = definition.ToStatus;

            // ── 2. Role check ─────────────────────────────────────────────────
            if (definition.RequiredRoles.Count > 0)
            {
                var hasRole = definition.RequiredRoles.Any(
                    r => context.Roles.Contains(r, StringComparer.OrdinalIgnoreCase));
                if (!hasRole)
                {
                    _logger.LogWarning(
                        "Transition {Code} denied: caller lacks any of required roles [{RequiredRoles}]. Case {CaseId} status {Status}",
                        definition.Code, string.Join(", ", definition.RequiredRoles), caseData.CaseId, fromStatus);
                    return TransitionExecutionResult.RoleDenied(
                        definition.Code, fromStatus, definition.RequiredRoles);
                }
            }

            // ── 3. Gate validation ────────────────────────────────────────────
            var gateResult = await _gateValidation.ValidateAsync(caseData, definition, context, ct);
            if (!gateResult.IsValid)
            {
                _logger.LogWarning(
                    "Transition {Code} gate checks failed for case {CaseId} status {Status}: {Summary}",
                    definition.Code, caseData.CaseId, fromStatus, gateResult.ToSummary());
                return TransitionExecutionResult.GateCheckFailed(
                    definition.Code, fromStatus, gateResult);
            }
        }
        else if (fallbackToStatus.HasValue)
        {
            // Catalog miss: use caller-supplied target status, skip gate checks.
            // This backward-compatible path allows migration of legacy trigger names.
            toStatus = fallbackToStatus.Value;
        }
        else
        {
            _logger.LogDebug(
                "Transition trigger '{Trigger}' not found in catalog for case {CaseId} status {Status}",
                triggerName, caseData.CaseId, fromStatus);
            return TransitionExecutionResult.NotFound(fromStatus, triggerName);
        }

        // ── 4. Apply transition ───────────────────────────────────────────────
        var now = DateTimeOffset.UtcNow;
        caseData.CurrentStatus = toStatus;
        caseData.StatusVersion += 1;
        caseData.UpdatedAt = now;

        // Write the mutated CaseData back to the tracked EF entity so that
        // SaveChangesAsync (called by the outer service) persists the new status.
        await _dataAccess.UpdateCaseAsync(caseData, ct);
        _logger.LogInformation(
            "Case {CaseId} transitioned {From} → {To} via '{Trigger}' (code: {Code}, catalogMatched: {Matched})",
            caseData.CaseId, fromStatus, toStatus, triggerName, transitionCode ?? "(fallback)", catalogMatched);
        // ── 5. Persist AuditLog ───────────────────────────────────────────────
        await _dataAccess.AddAuditLogAsync(BuildAuditLog(
            caseData.CaseId, triggerName, context, fromStatus, toStatus,
            transitionCode, catalogMatched, definition?.GateChecks, now), ct);

        // ── 6. Persist CaseTransitionHistory ─────────────────────────────────
        await _dataAccess.AddCaseTransitionHistoryAsync(BuildHistory(
            caseData.CaseId, triggerName, context, fromStatus, toStatus,
            transitionCode, catalogMatched, now), ct);

        // ── 7. Execute transition side effects (catalog-matched only) ─────────
        if (definition is not null)
        {
            await _sideEffects.ExecuteAsync(
                definition,
                new SideEffectContext { CaseData = caseData, ValidationContext = context, Now = now },
                ct);
        }

        activity?.SetTag(WfmgrActivitySource.TagTransitionCode, transitionCode ?? "(fallback)");
        activity?.SetTag(WfmgrActivitySource.TagToStatus, toStatus.ToString());
        activity?.SetTag(WfmgrActivitySource.TagResult, "succeeded");
        return TransitionExecutionResult.Succeeded(transitionCode, fromStatus, toStatus);
    }

    // ── Private builders ──────────────────────────────────────────────────────

    private static AuditLogData BuildAuditLog(
        Guid caseId,
        string triggerName,
        GateValidationContext context,
        CaseStatus fromStatus,
        CaseStatus toStatus,
        string? transitionCode,
        bool catalogMatched,
        string[]? gateChecks,
        DateTimeOffset now) =>
        new()
        {
            AuditId = Guid.NewGuid(),
            CaseId = caseId,
            ActorType = context.Roles.Count > 0 ? "User" : "System",
            ActorId = context.UserId,
            Action = triggerName,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            SnapshotJson = JsonSerializer.Serialize(new
            {
                transitionCode,
                catalogMatched,
                gateChecks = gateChecks ?? [],
                reason = context.Reason,
                roles = context.Roles,
            }),
            CreatedAt = now,
        };

    private static CaseTransitionHistoryData BuildHistory(
        Guid caseId,
        string triggerName,
        GateValidationContext context,
        CaseStatus fromStatus,
        CaseStatus toStatus,
        string? transitionCode,
        bool catalogMatched,
        DateTimeOffset now) =>
        new()
        {
            TransitionId = Guid.NewGuid(),
            CaseId = caseId,
            FromStatus = fromStatus.ToString(),
            ToStatus = toStatus.ToString(),
            TriggerType = context.Roles.Count > 0 ? "User" : "System",
            TriggerName = triggerName,
            TriggeredBy = context.UserId,
            Reason = context.Reason,
            MetadataJson = JsonSerializer.Serialize(new
            {
                transitionCode,
                catalogMatched,
                roles = context.Roles,
                formId = context.FormId,
                workItemId = context.WorkItemId,
                externalEventPresent = !string.IsNullOrWhiteSpace(context.ExternalEventPayload),
            }),
            CreatedAt = now,
        };
}
