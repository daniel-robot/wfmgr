using Microsoft.Extensions.Logging;
using Wfmgr.Engine.Abstractions;
using Wfmgr.Engine.Core;

namespace Wfmgr.Engine.Core;

/// <summary>
/// Engine-level transition orchestrator.
/// <para>
/// Execution pipeline:
/// 1. Catalog lookup by triggerName + fromStatus.
/// 2. RequiredRoles check.
/// 3. Gate validation via host-provided <see cref="IGateValidationService"/>.
/// 4. Persist status change via host <see cref="IWorkflowDataAccess"/>.
/// 5. Audit log and transition history via host data access.
/// 6. Side effects via host <see cref="ISideEffectService"/>.
/// </para>
/// </summary>
public sealed class TransitionEngine : ITransitionEngine
{
    private readonly ITransitionCatalogService _catalog;
    private readonly IGateValidationService _gateValidation;
    private readonly ISideEffectService _sideEffects;
    private readonly IWorkflowDataAccess _dataAccess;
    private readonly ILogger<TransitionEngine> _logger;

    public TransitionEngine(
        ITransitionCatalogService catalog,
        IGateValidationService gateValidation,
        ISideEffectService sideEffects,
        IWorkflowDataAccess dataAccess,
        ILogger<TransitionEngine> logger)
    {
        _catalog = catalog;
        _gateValidation = gateValidation;
        _sideEffects = sideEffects;
        _dataAccess = dataAccess;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TransitionExecutionResult> ApplyTransitionAsync(
        IWorkflowSubject subject,
        string triggerName,
        GateValidationContext context,
        CancellationToken ct = default,
        string? fallbackToStatus = null)
    {
        var fromStatus = subject.CurrentStatus;

        var definition = await _catalog.FindByTriggerAsync(triggerName, fromStatus, ct);

        string toStatus;
        string? transitionCode = null;

        if (definition is not null)
        {
            transitionCode = definition.Code;
            toStatus = definition.ToStatus;

            // ── Role check ─────────────────────────────────────────────
            if (definition.RequiredRoles.Count > 0)
            {
                var hasRole = definition.RequiredRoles.Any(
                    r => context.Roles.Contains(r, StringComparer.OrdinalIgnoreCase));
                if (!hasRole)
                {
                    _logger.LogWarning(
                        "Transition {Code} denied: caller lacks any of required roles [{RequiredRoles}]. Subject {SubjectId} status {Status}",
                        definition.Code, string.Join(", ", definition.RequiredRoles), subject.SubjectId, fromStatus);
                    return TransitionExecutionResult.RoleDenied(
                        definition.Code, fromStatus, definition.RequiredRoles);
                }
            }

            // ── Gate validation ────────────────────────────────────────
            var gateResult = await _gateValidation.ValidateAsync(subject, definition, context, ct);
            if (!gateResult.IsValid)
            {
                _logger.LogWarning(
                    "Transition {Code} gate checks failed for subject {SubjectId} status {Status}: {Summary}",
                    definition.Code, subject.SubjectId, fromStatus, gateResult.ToSummary());
                return TransitionExecutionResult.GateCheckFailed(
                    definition.Code, fromStatus, gateResult);
            }
        }
        else if (fallbackToStatus is not null)
        {
            toStatus = fallbackToStatus;
        }
        else
        {
            _logger.LogDebug(
                "Transition trigger '{Trigger}' not found in catalog for subject {SubjectId} status {Status}",
                triggerName, subject.SubjectId, fromStatus);
            return TransitionExecutionResult.NotFound(fromStatus, triggerName);
        }

        // ── Apply transition (status mutation) ─────────────────────────
        var newVersion = subject.StatusVersion + 1;
        await _dataAccess.UpdateSubjectStatusAsync(subject, toStatus, newVersion, ct);

        _logger.LogInformation(
            "Subject {SubjectId} transitioned {From} → {To} via '{Trigger}' (code: {Code})",
            subject.SubjectId, fromStatus, toStatus, triggerName, transitionCode ?? "(fallback)");

        // ── Audit log ──────────────────────────────────────────────────
        await _dataAccess.AddAuditLogAsync(new EngineAuditLogEntry
        {
            SubjectId = subject.SubjectId,
            ActorType = context.Roles.Count > 0 ? "User" : "System",
            ActorId = context.UserId,
            Action = triggerName,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            SnapshotJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                transitionCode,
                catalogMatched = definition is not null,
                gateChecks = definition?.GateChecks ?? [],
                reason = context.Reason,
                roles = context.Roles,
            }),
            CreatedAt = DateTimeOffset.UtcNow,
        }, ct);

        // ── Transition history ─────────────────────────────────────────
        await _dataAccess.AddTransitionHistoryAsync(new EngineTransitionHistoryEntry
        {
            SubjectId = subject.SubjectId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            TriggerType = context.Roles.Count > 0 ? "User" : "System",
            TriggerName = triggerName,
            TriggeredBy = context.UserId,
            Reason = context.Reason,
            MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                transitionCode,
                catalogMatched = definition is not null,
                roles = context.Roles,
            }),
            CreatedAt = DateTimeOffset.UtcNow,
        }, ct);

        // ── Side effects (catalog-matched only) ────────────────────────
        if (definition is not null)
        {
            await _sideEffects.ExecuteAsync(
                definition,
                new SideEffectContext
                {
                    Subject = subject,
                    ValidationContext = context,
                    Transition = definition,
                    Now = DateTimeOffset.UtcNow,
                },
                ct);
        }

        return TransitionExecutionResult.Succeeded(transitionCode, fromStatus, toStatus);
    }

    /// <inheritdoc/>
    public async Task<TransitionExecutionResult> ApplyTransitionAsync(
        string subjectId,
        string triggerName,
        GateValidationContext context,
        CancellationToken ct = default,
        string? fallbackToStatus = null)
    {
        var subject = await _dataAccess.GetSubjectAsync(subjectId, ct)
            ?? throw new InvalidOperationException($"Subject '{subjectId}' not found.");

        return await ApplyTransitionAsync(subject, triggerName, context, ct, fallbackToStatus);
    }
}
