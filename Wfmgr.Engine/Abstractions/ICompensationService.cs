using Wfmgr.Engine.Core;

namespace Wfmgr.Engine.Abstractions;

/// <summary>
/// Manages compensation (rollback) for workflow transitions.
/// The host provides its own implementation with domain-specific compensation logic.
/// </summary>
public interface ICompensationService
{
    Task<CompensationResult> CompensateAsync(
        IWorkflowSubject subject,
        CompensationContext context,
        CancellationToken ct);
}
