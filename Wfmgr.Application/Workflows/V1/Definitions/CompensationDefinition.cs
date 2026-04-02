using Wfmgr.Domain.Enums;

namespace Wfmgr.Application.Workflows.V1.Definitions;

/// <summary>
/// Static catalog record that describes how the system should respond when a workflow
/// step fails or produces an invalid outcome.
/// <para>
/// A <see cref="CompensationDefinition"/> names the failed transition step, the failure
/// condition, the compensating action to take, an optional target status to restore the
/// case to, an optional work item to create, and retry guidance.
/// </para>
/// <para>
/// These objects are pure metadata.  Compensation execution logic lives in the
/// application service layer and is not yet implemented.
/// </para>
/// </summary>
public sealed class CompensationDefinition
{
    /// <summary>Unique business code for this compensation rule (e.g. "CMP-001").</summary>
    public required string Code { get; init; }

    /// <summary>Transition code of the step that produced the failure (e.g. "IMG-002").</summary>
    public required string FailedStepCode { get; init; }

    /// <summary>Human-readable description of the failure condition that triggers this rule.</summary>
    public required string FailureCondition { get; init; }

    /// <summary>Description of the compensating action the system or operator should take.</summary>
    public required string CompensationAction { get; init; }

    /// <summary>
    /// The <see cref="CaseStatus"/> to restore the case to after compensation.
    /// <c>null</c> means the case status must remain unchanged (e.g. for idempotent duplicate handling).
    /// </summary>
    public CaseStatus? TargetStatus { get; init; }

    /// <summary>
    /// <see cref="Domain.WorkItems.WorkItemTypes"/> name of the work item to create as part
    /// of the compensation; <c>null</c> if no work item is needed.
    /// </summary>
    public string? WorkItemToCreate { get; init; }

    /// <summary>Whether human intervention is required to fully resolve the failure.</summary>
    public bool ManualInterventionRequired { get; init; }

    /// <summary>
    /// Optional retry guidance for automated recovery attempts.
    /// <c>null</c> means no automated retries are supported for this failure type.
    /// </summary>
    public RetryPolicy? RetryPolicy { get; init; }
}

/// <summary>
/// Describes how an operation should be retried after a transient failure.
/// </summary>
/// <param name="Strategy">
/// Named retry strategy identifier (e.g. "ExponentialBackoff", "LimitedRetry", "TimerEscalation").
/// </param>
/// <param name="MaxAttempts">Maximum number of retry attempts before giving up.</param>
/// <param name="InitialDelay">
/// Base delay before the first retry.  Actual inter-attempt delay may vary depending on
/// the strategy implementation (e.g. exponential growth).
/// </param>
public sealed record RetryPolicy(
    string Strategy,
    int MaxAttempts = 3,
    TimeSpan? InitialDelay = null)
{
    /// <summary>
    /// Exponential back-off starting at 30 seconds, up to 5 attempts.
    /// Used for outbox/integration send failures where temporary unavailability is expected.
    /// </summary>
    public static readonly RetryPolicy ExponentialBackoff =
        new("ExponentialBackoff", MaxAttempts: 5, InitialDelay: TimeSpan.FromSeconds(30));

    /// <summary>
    /// Fixed-interval limited retry with a 60-second delay, up to 3 attempts.
    /// Used for integration calls that are unlikely to self-heal quickly.
    /// </summary>
    public static readonly RetryPolicy LimitedRetry =
        new("LimitedRetry", MaxAttempts: 3, InitialDelay: TimeSpan.FromMinutes(1));

    /// <summary>
    /// Single-shot timer-based escalation triggered after a prolonged idle period (4 hours).
    /// Used when a case is paused or interrupted and must be escalated if not resolved in time.
    /// </summary>
    public static readonly RetryPolicy TimerEscalation =
        new("TimerEscalation", MaxAttempts: 1, InitialDelay: TimeSpan.FromHours(4));
}
