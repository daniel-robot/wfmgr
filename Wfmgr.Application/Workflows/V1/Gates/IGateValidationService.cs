using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Application.Workflows.V1.Definitions;

namespace Wfmgr.Application.Workflows.V1.Gates;

/// <summary>
/// Evaluates the named gate checks declared on a <see cref="TransitionDefinition"/>
/// against the current case state and the caller-supplied execution context.
/// </summary>
public interface IGateValidationService
{
    /// <summary>
    /// Runs every gate check listed in <paramref name="transition"/>.<see cref="TransitionDefinition.GateChecks"/>
    /// and returns a <see cref="GateValidationResult"/> that describes all failures.
    /// </summary>
    /// <param name="caseData">Current persistent state of the workflow case.</param>
    /// <param name="transition">The transition whose gate checks are to be validated.</param>
    /// <param name="context">
    /// Caller-supplied execution context (user identity, form ID, event payload, etc.).
    /// Use <see cref="GateValidationContext.System"/> for fully automated system transitions.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="GateValidationResult"/> that is valid when all checks pass, or contains
    /// the names and messages of every failed check.
    /// </returns>
    Task<GateValidationResult> ValidateAsync(
        CaseData caseData,
        TransitionDefinition transition,
        GateValidationContext context,
        CancellationToken ct = default);
}
