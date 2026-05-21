using Wfmgr.Application.Workflows.V1.Definitions;

namespace Wfmgr.Application.Workflows.V1.SideEffects;

/// <summary>
/// Extension point for adding host-provided side-effect handlers to the workflow
/// engine. Each handler advertises a stable <see cref="Name"/> that matches an
/// entry in <see cref="TransitionDefinition.SuccessActions"/>; when the engine
/// processes a transition it invokes every registered handler whose name appears
/// in the success-actions list.
/// <para>
/// Built-in side effects (work-item creation and outbox dispatch) continue to run
/// unchanged. Host handlers run <em>after</em> the built-ins so they can react to
/// the post-transition state.
/// </para>
/// </summary>
public interface ISideEffectHandler
{
    /// <summary>
    /// Stable identifier matching a <see cref="TransitionDefinition.SuccessActions"/>
    /// entry. Comparison is case-insensitive.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the side effect for the supplied transition and execution context.
    /// Implementations should be idempotent: a transition may be replayed.
    /// </summary>
    Task ExecuteAsync(
        TransitionDefinition definition,
        SideEffectContext context,
        CancellationToken ct);
}
