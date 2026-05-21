using Wfmgr.Application.Abstractions.Persistence.Models;

namespace Wfmgr.Application.Workflows.V1.Gates;

/// <summary>
/// Extension point for adding host-provided gate checks to the workflow engine.
/// <para>
/// Each evaluator advertises a stable <see cref="Name"/> (matching a string value
/// referenced by a <see cref="Definitions.TransitionDefinition.GateChecks"/> entry)
/// and an <see cref="EvaluateAsync"/> method that returns <c>null</c> when the gate
/// passes, or a human-readable failure message when it does not.
/// </para>
/// <para>
/// All <see cref="IGateEvaluator"/> instances registered in DI are merged into the
/// dispatch map of <see cref="GateValidationService"/>. Host-provided evaluators win
/// over built-in checks of the same name, so applications can override engine
/// defaults without modifying engine code.
/// </para>
/// </summary>
public interface IGateEvaluator
{
    /// <summary>
    /// Stable identifier matching the gate-check name used in transition definitions.
    /// Comparison is case-insensitive.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates the gate against the supplied case state and execution context.
    /// </summary>
    /// <returns>
    /// <c>null</c> when the gate passes; otherwise a human-readable failure message.
    /// </returns>
    Task<string?> EvaluateAsync(
        CaseData caseData,
        GateValidationContext context,
        CancellationToken ct);
}
