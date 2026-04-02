namespace Wfmgr.Application.Workflows.V1.Gates;

/// <summary>
/// The result of evaluating all gate checks declared on a
/// <see cref="Definitions.TransitionDefinition"/> via <see cref="IGateValidationService"/>.
/// </summary>
public sealed class GateValidationResult
{
    private GateValidationResult(
        bool isValid,
        IReadOnlyList<string> failedChecks,
        IReadOnlyList<string> messages)
    {
        IsValid = isValid;
        FailedChecks = failedChecks;
        Messages = messages;
    }

    /// <summary><c>true</c> when all gate checks passed; <c>false</c> if one or more failed.</summary>
    public bool IsValid { get; }

    /// <summary>
    /// Names of the gate checks that did not pass.
    /// Empty when <see cref="IsValid"/> is <c>true</c>.
    /// </summary>
    public IReadOnlyList<string> FailedChecks { get; }

    /// <summary>
    /// Human-readable failure messages in the same order as <see cref="FailedChecks"/>.
    /// Each message describes why the corresponding check failed.
    /// </summary>
    public IReadOnlyList<string> Messages { get; }

    /// <summary>
    /// Constructs a fully successful result with no failed checks or messages.
    /// </summary>
    public static GateValidationResult Success() =>
        new(true, [], []);

    /// <summary>
    /// Constructs a failed result from pre-populated collections.
    /// </summary>
    /// <param name="failedChecks">Names of checks that failed.</param>
    /// <param name="messages">Corresponding human-readable failure messages.</param>
    public static GateValidationResult Failure(
        IReadOnlyList<string> failedChecks,
        IReadOnlyList<string> messages) =>
        new(false, failedChecks, messages);

    /// <summary>
    /// Constructs a failed result for a single check failure.
    /// </summary>
    public static GateValidationResult SingleFailure(string checkName, string message) =>
        new(false, [checkName], [message]);

    /// <summary>
    /// Returns a formatted summary of all failures, useful for logging or exception messages.
    /// </summary>
    public string ToSummary() =>
        IsValid
            ? "All gate checks passed."
            : $"Gate check(s) failed: {string.Join("; ", Messages)}";
}
