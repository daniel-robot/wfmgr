using Wfmgr.Application.Workflows.V1.Gates;
using Wfmgr.Domain.Enums;

namespace Wfmgr.Application.Workflows.V1;

/// <summary>
/// Describes why an attempted workflow transition was rejected.
/// </summary>
public enum TransitionFailureReason
{
    /// <summary>
    /// No <see cref="TransitionDefinition"/> in the catalog matches the supplied trigger name
    /// and current case status, and no fallback target status was provided.
    /// </summary>
    NotFound,

    /// <summary>
    /// The actor's roles do not satisfy <see cref="Definitions.TransitionDefinition.RequiredRoles"/>.
    /// </summary>
    RoleDenied,

    /// <summary>
    /// One or more named gate checks declared on the <see cref="Definitions.TransitionDefinition"/>
    /// returned a failure from <see cref="IGateValidationService"/>.
    /// </summary>
    GateCheckFailed,
}

/// <summary>
/// The result of a workflow transition attempt executed via <see cref="ICaseTransitionService"/>.
/// <para>
/// On success, <see cref="IsSuccess"/> is <c>true</c>, <see cref="ToStatus"/> is the new case
/// status, and <see cref="FailedChecks"/> / <see cref="Messages"/> are empty.
/// </para>
/// <para>
/// On failure, <see cref="FailureReason"/> identifies the category of failure, and
/// <see cref="Messages"/> provides human-readable details for logging or API responses.
/// </para>
/// </summary>
public sealed class TransitionExecutionResult
{
    private TransitionExecutionResult(
        bool isSuccess,
        string? transitionCode,
        CaseStatus fromStatus,
        CaseStatus? toStatus,
        TransitionFailureReason? failureReason,
        IReadOnlyList<string> failedChecks,
        IReadOnlyList<string> messages)
    {
        IsSuccess = isSuccess;
        TransitionCode = transitionCode;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        FailureReason = failureReason;
        FailedChecks = failedChecks;
        Messages = messages;
    }

    /// <summary><c>true</c> when the transition was applied and state was mutated.</summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Business code of the matched <see cref="Definitions.TransitionDefinition"/> (e.g. "SIM-001"),
    /// or <c>null</c> when the transition was applied via the fallback path (no catalog entry found).
    /// </summary>
    public string? TransitionCode { get; }

    /// <summary>The case status at the time the transition was attempted.</summary>
    public CaseStatus FromStatus { get; }

    /// <summary>
    /// The status the case was moved to on success, or <c>null</c> on failure.
    /// </summary>
    public CaseStatus? ToStatus { get; }

    /// <summary>Category of failure, or <c>null</c> on success.</summary>
    public TransitionFailureReason? FailureReason { get; }

    /// <summary>
    /// Names of the gate checks that failed.
    /// Empty on success or when the failure is not <see cref="TransitionFailureReason.GateCheckFailed"/>.
    /// </summary>
    public IReadOnlyList<string> FailedChecks { get; }

    /// <summary>Human-readable failure messages in the same order as <see cref="FailedChecks"/>.</summary>
    public IReadOnlyList<string> Messages { get; }

    // ── Factory methods ───────────────────────────────────────────────────────

    internal static TransitionExecutionResult Succeeded(
        string? transitionCode,
        CaseStatus fromStatus,
        CaseStatus toStatus) =>
        new(true, transitionCode, fromStatus, toStatus, null, [], []);

    internal static TransitionExecutionResult NotFound(CaseStatus fromStatus, string triggerName) =>
        new(false, null, fromStatus, null,
            TransitionFailureReason.NotFound,
            [],
            [$"No transition definition found for trigger '{triggerName}' from status '{fromStatus}'."]);

    internal static TransitionExecutionResult RoleDenied(
        string? transitionCode,
        CaseStatus fromStatus,
        IReadOnlyList<string> requiredRoles) =>
        new(false, transitionCode, fromStatus, null,
            TransitionFailureReason.RoleDenied,
            [],
            [$"Transition requires one of roles: {string.Join(", ", requiredRoles)}."]);

    internal static TransitionExecutionResult GateCheckFailed(
        string? transitionCode,
        CaseStatus fromStatus,
        GateValidationResult gateResult) =>
        new(false, transitionCode, fromStatus, null,
            TransitionFailureReason.GateCheckFailed,
            gateResult.FailedChecks,
            gateResult.Messages);

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> when the transition failed.
    /// Useful for callers that enforce success (throws immediately, retaining full context).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="IsSuccess"/> is <c>false</c>.</exception>
    public void ThrowIfFailed()
    {
        if (!IsSuccess)
        {
            throw new InvalidOperationException(ToSummary());
        }
    }

    /// <summary>Returns a formatted one-line summary for logging or exception messages.</summary>
    public string ToSummary() =>
        IsSuccess
            ? $"Transition '{TransitionCode ?? "(no code)"}' succeeded: {FromStatus} → {ToStatus}."
            : $"Transition failed [{FailureReason}]: {string.Join("; ", Messages)}";
}
