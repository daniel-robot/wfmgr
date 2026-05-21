using Wfmgr.Engine.Core;

namespace Wfmgr.Engine.Abstractions;

/// <summary>
/// Evaluates gate checks declared on a <see cref="TransitionDefinition"/>.
/// The host provides its own implementation with domain-specific gate check logic.
/// </summary>
public interface IGateValidationService
{
    Task<GateValidationResult> ValidateAsync(
        IWorkflowSubject subject,
        TransitionDefinition transition,
        GateValidationContext context,
        CancellationToken ct = default);
}
