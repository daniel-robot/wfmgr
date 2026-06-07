using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Domain.Enums;

namespace Wfmgr.Application.Workflows.V1.Compensation;

/// <summary>Indicates why compensation could not be applied.</summary>
public enum CompensationFailureReason
{
    /// <summary>No <see cref="CompensationDefinition"/> matched the supplied <c>failedStepCode</c>.</summary>
    DefinitionNotFound,

    /// <summary>The case was not found in the data store.</summary>
    CaseNotFound,

    /// <summary>The engine rejected the compensation status transition (role, gate, or lookup failure).</summary>
    TransitionFailed,

    /// <summary>A work item could not be created because a required dependency was missing.</summary>
    WorkItemCreationFailed,
}

/// <summary>
/// Returned by <see cref="IWorkflowCompensationService.HandleFailureAsync"/> to describe
/// the outcome of a compensation attempt.
/// </summary>
public sealed class CompensationResult
{
    /// <summary><c>true</c> when compensation was applied successfully.</summary>
    public bool IsSuccess { get; private init; }

    /// <summary>Code of the matched <see cref="CompensationDefinition"/>, e.g. "CMP-008".</summary>
    public string? CompensationCode { get; private init; }

    /// <summary>Case status before compensation was applied.</summary>
    public CaseStatus? PreviousStatus { get; private init; }

    /// <summary>Case status after compensation (<c>null</c> when status was not changed).</summary>
    public CaseStatus? NewStatus { get; private init; }

    /// <summary>Type of the work item created, or <c>null</c> if none was created.</summary>
    public string? WorkItemCreated { get; private init; }

    /// <summary>
    /// Whether at least one retry outbox message was enqueued as part of the compensation.
    /// </summary>
    public bool RetryDispatched { get; private init; }

    /// <summary>Set when <see cref="IsSuccess"/> is <c>false</c>.</summary>
    public CompensationFailureReason? FailureReason { get; private init; }

    /// <summary>Additional human-readable detail about the failure.</summary>
    public string? FailureDetail { get; private init; }

    // ── Factories ─────────────────────────────────────────────────────────────

    internal static CompensationResult Succeeded(
        string compensationCode,
        CaseStatus? previousStatus,
        CaseStatus? newStatus,
        string? workItemCreated,
        bool retryDispatched) =>
        new()
        {
            IsSuccess = true,
            CompensationCode = compensationCode,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            WorkItemCreated = workItemCreated,
            RetryDispatched = retryDispatched,
        };

    internal static CompensationResult Failed(
        CompensationFailureReason reason,
        string detail) =>
        new()
        {
            IsSuccess = false,
            FailureReason = reason,
            FailureDetail = detail,
        };

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Throws <see cref="InvalidOperationException"/> if the result is a failure.</summary>
    public void ThrowIfFailed()
    {
        if (!IsSuccess)
            throw new InvalidOperationException(
                $"Compensation failed [{FailureReason}]: {FailureDetail}");
    }

    /// <summary>Returns a one-line summary suitable for logging.</summary>
    public string ToSummary() =>
        IsSuccess
            ? $"Compensation {CompensationCode} applied. " +
              $"Status: {PreviousStatus} → {NewStatus?.ToString() ?? "(unchanged)"}. " +
              $"WorkItem: {WorkItemCreated ?? "none"}. RetryEnqueued: {RetryDispatched}."
            : $"Compensation failed [{FailureReason}]: {FailureDetail}";
}
