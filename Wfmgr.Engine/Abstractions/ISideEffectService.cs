using Wfmgr.Engine.Core;

namespace Wfmgr.Engine.Abstractions;

/// <summary>
/// Executes side effects declared on a <see cref="TransitionDefinition"/> after a successful transition.
/// The host provides its own implementation.
/// </summary>
public interface ISideEffectService
{
    Task ExecuteAsync(
        TransitionDefinition transition,
        SideEffectContext context,
        CancellationToken ct);
}
