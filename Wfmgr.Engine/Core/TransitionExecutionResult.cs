namespace Wfmgr.Engine.Core;

/// <summary>
/// Describes why a workflow transition attempt was rejected.
/// </summary>
public enum TransitionFailureReason
{
    NotFound,
    RoleDenied,
    GateCheckFailed,
}

/// <summary>
/// The result of a workflow transition attempt.
/// Status values are represented as strings, making this engine-agnostic.
/// </summary>
public sealed class TransitionExecutionResult
{
    private TransitionExecutionResult(
        bool isSuccess,
        string? transitionCode,
        string fromStatus,
        string? toStatus,
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

    public bool IsSuccess { get; }
    public string? TransitionCode { get; }
    public string FromStatus { get; }
    public string? ToStatus { get; }
    public TransitionFailureReason? FailureReason { get; }
    public IReadOnlyList<string> FailedChecks { get; }
    public IReadOnlyList<string> Messages { get; }

    public static TransitionExecutionResult Succeeded(
        string? transitionCode, string fromStatus, string toStatus) =>
        new(true, transitionCode, fromStatus, toStatus, null, [], []);

    public static TransitionExecutionResult NotFound(string fromStatus, string triggerName) =>
        new(false, null, fromStatus, null,
            TransitionFailureReason.NotFound,
            [],
            [$"No transition definition found for trigger '{triggerName}' from status '{fromStatus}'."]);

    public static TransitionExecutionResult RoleDenied(
        string? transitionCode, string fromStatus, IReadOnlyList<string> requiredRoles) =>
        new(false, transitionCode, fromStatus, null,
            TransitionFailureReason.RoleDenied,
            [],
            [$"Transition requires one of roles: {string.Join(", ", requiredRoles)}."]);

    public static TransitionExecutionResult GateCheckFailed(
        string? transitionCode, string fromStatus, GateValidationResult gateResult) =>
        new(false, transitionCode, fromStatus, null,
            TransitionFailureReason.GateCheckFailed,
            gateResult.FailedChecks,
            gateResult.Messages);

    public void ThrowIfFailed()
    {
        if (!IsSuccess)
            throw new InvalidOperationException(ToSummary());
    }

    public string ToSummary() =>
        IsSuccess
            ? $"Transition '{TransitionCode ?? "(no code)"}' succeeded: {FromStatus} → {ToStatus}."
            : $"Transition failed [{FailureReason}]: {string.Join("; ", Messages)}";
}
