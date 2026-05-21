using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Workflows.V1.Config;
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Application.Workflows.V1.Gates;

namespace Wfmgr.Application.Workflows.V1;

/// <summary>
/// Read-only "what would happen" service for the workflow engine.
/// <para>
/// Returns the full catalog of transition definitions (for documentation / UI) and evaluates
/// a hypothetical transition against the current case state without writing audit, history,
/// or side-effects. Reuses <see cref="IGateValidationService"/> so behaviour matches the
/// real <see cref="ICaseTransitionService"/> pipeline.
/// </para>
/// </summary>
public interface IWorkflowExplainService
{
    Task<IReadOnlyList<TransitionDefinitionDto>> GetCatalogAsync(CancellationToken ct);

    Task<ExplainTransitionResponse> ExplainAsync(ExplainTransitionRequest request, CancellationToken ct);
}

public sealed class WorkflowExplainService : IWorkflowExplainService
{
    private readonly IWorkflowDataAccess _dataAccess;
    private readonly IGateValidationService _gateValidation;
    private readonly IWorkflowTransitionCatalogService _catalog;

    public WorkflowExplainService(
        IWorkflowDataAccess dataAccess,
        IGateValidationService gateValidation,
        IWorkflowTransitionCatalogService catalog)
    {
        _dataAccess = dataAccess;
        _gateValidation = gateValidation;
        _catalog = catalog;
    }

    public async Task<IReadOnlyList<TransitionDefinitionDto>> GetCatalogAsync(CancellationToken ct)
    {
        var all = await _catalog.GetAllAsync(ct);
        return all.Select(ToDto).ToList();
    }

    public async Task<ExplainTransitionResponse> ExplainAsync(ExplainTransitionRequest request, CancellationToken ct)
    {
        var roles = request.Roles ?? [];
        var caseData = await _dataAccess.GetCaseByIdAsync(request.CaseId, ct);
        if (caseData is null)
        {
            return new ExplainTransitionResponse(
                request.CaseId,
                request.TriggerName,
                CurrentStatus: "(case not found)",
                MatchFound: false,
                MatchedTransition: null,
                RoleCheckPassed: false,
                RolesProvided: roles,
                RequiredRoles: [],
                GateChecksPassed: false,
                GateResults: [],
                WouldTransition: false,
                Notes: $"Case '{request.CaseId}' not found.");
        }

        var fromStatus = caseData.CurrentStatus;
        var definition = await _catalog.FindByTriggerAsync(request.TriggerName, fromStatus.ToString(), ct);

        if (definition is null)
        {
            return new ExplainTransitionResponse(
                request.CaseId,
                request.TriggerName,
                CurrentStatus: fromStatus.ToString(),
                MatchFound: false,
                MatchedTransition: null,
                RoleCheckPassed: false,
                RolesProvided: roles,
                RequiredRoles: [],
                GateChecksPassed: false,
                GateResults: [],
                WouldTransition: false,
                Notes: $"No transition matches trigger '{request.TriggerName}' from status '{fromStatus}'.");
        }

        // Role check (mirrors CaseTransitionService logic exactly).
        var roleCheckPassed = definition.RequiredRoles.Count == 0
            || definition.RequiredRoles.Any(r => roles.Contains(r, StringComparer.OrdinalIgnoreCase));

        var ctx = new GateValidationContext
        {
            UserId = null,
            Roles = roles,
            Reason = request.Reason,
        };

        var gateResult = await _gateValidation.ValidateAsync(caseData, definition, ctx, ct);
        var failedSet = new HashSet<string>(gateResult.FailedChecks, StringComparer.Ordinal);
        var failureMsgByCheck = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < gateResult.FailedChecks.Count && i < gateResult.Messages.Count; i++)
        {
            failureMsgByCheck[gateResult.FailedChecks[i]] = gateResult.Messages[i];
        }

        var gateResults = definition.GateChecks
            .Select(name => new ExplainGateResultDto(
                name,
                Passed: !failedSet.Contains(name),
                Message: failureMsgByCheck.TryGetValue(name, out var msg) ? msg : null))
            .ToList();

        var wouldTransition = roleCheckPassed && gateResult.IsValid;
        var notes = wouldTransition
            ? $"Would transition {fromStatus} → {definition.ToStatus} via '{definition.Code}'."
            : !roleCheckPassed
                ? $"Role check failed: caller roles [{string.Join(", ", roles)}] do not include any of [{string.Join(", ", definition.RequiredRoles)}]."
                : "One or more gate checks failed (see GateResults).";

        return new ExplainTransitionResponse(
            request.CaseId,
            request.TriggerName,
            CurrentStatus: fromStatus.ToString(),
            MatchFound: true,
            MatchedTransition: ToDto(definition),
            RoleCheckPassed: roleCheckPassed,
            RolesProvided: roles,
            RequiredRoles: definition.RequiredRoles,
            GateChecksPassed: gateResult.IsValid,
            GateResults: gateResults,
            WouldTransition: wouldTransition,
            Notes: notes);
    }

    private static TransitionDefinitionDto ToDto(TransitionDefinition d) => new(
        d.Code,
        d.TriggerName,
        d.TriggerType.ToString(),
        d.FromStatuses.Select(s => s.ToString()).ToList(),
        d.ToStatus.ToString(),
        d.RequiredRoles,
        d.GateChecks,
        d.SuccessActions,
        d.FailureActions,
        d.WorkItemsToCreate,
        d.ConfigSlot);
}
