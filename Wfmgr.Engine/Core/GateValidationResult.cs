namespace Wfmgr.Engine.Core;

/// <summary>
/// Result of validating all gate checks for a transition.
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

    public bool IsValid { get; }
    public IReadOnlyList<string> FailedChecks { get; }
    public IReadOnlyList<string> Messages { get; }

    public static GateValidationResult Success() =>
        new(true, [], []);

    public static GateValidationResult Failure(
        IReadOnlyList<string> failedChecks,
        IReadOnlyList<string> messages) =>
        new(false, failedChecks, messages);

    public string ToSummary() =>
        IsValid ? "All gate checks passed." : $"Gate checks failed: {string.Join("; ", Messages)}";
}
