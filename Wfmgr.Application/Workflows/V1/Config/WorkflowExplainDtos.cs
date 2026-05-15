namespace Wfmgr.Application.Workflows.V1.Config;

/// <summary>
/// Read-only projection of a <c>TransitionDefinition</c> for explainability/admin endpoints.
/// </summary>
public sealed record TransitionDefinitionDto(
    string Code,
    string TriggerName,
    string TriggerType,
    IReadOnlyList<string> FromStatuses,
    string ToStatus,
    IReadOnlyList<string> RequiredRoles,
    IReadOnlyList<string> GateChecks,
    IReadOnlyList<string> SuccessActions,
    IReadOnlyList<string> FailureActions,
    IReadOnlyList<string> WorkItemsToCreate,
    string? ConfigSlot);

/// <summary>
/// Dry-run explainability request — describes a hypothetical transition attempt
/// without mutating any state.
/// </summary>
public sealed record ExplainTransitionRequest(
    Guid CaseId,
    string TriggerName,
    IReadOnlyList<string>? Roles,
    string? Reason);

/// <summary>Result of evaluating a single gate check during a dry-run explain call.</summary>
public sealed record ExplainGateResultDto(
    string GateCheck,
    bool Passed,
    string? Message);

/// <summary>
/// Read-only response describing whether a transition would be allowed and, if not, why.
/// No <c>AuditLog</c>, <c>CaseTransitionHistory</c>, or side-effects are written.
/// </summary>
public sealed record ExplainTransitionResponse(
    Guid CaseId,
    string TriggerName,
    string CurrentStatus,
    bool MatchFound,
    TransitionDefinitionDto? MatchedTransition,
    bool RoleCheckPassed,
    IReadOnlyList<string> RolesProvided,
    IReadOnlyList<string> RequiredRoles,
    bool GateChecksPassed,
    IReadOnlyList<ExplainGateResultDto> GateResults,
    bool WouldTransition,
    string? Notes);
