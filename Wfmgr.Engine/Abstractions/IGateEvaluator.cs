using Wfmgr.Engine.Core;

namespace Wfmgr.Engine.Abstractions;

/// <summary>
/// Host-provided pluggable gate evaluator.
/// The host registers named evaluators that the engine's gate validation service can invoke.
/// </summary>
public interface IGateEvaluator
{
    /// <summary>Unique name matching a gate check name in <c>TransitionDefinition.GateChecks</c>.</summary>
    string Name { get; }

    /// <summary>Evaluates the gate check. Returns null on pass, or a failure message on failure.</summary>
    Task<string?> EvaluateAsync(IWorkflowSubject subject, GateValidationContext context, CancellationToken ct);
}
