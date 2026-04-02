using Wfmgr.Application.Workflows.V1.Definitions;

namespace Wfmgr.Application.Workflows.V1.SideEffects;

/// <summary>
/// Processes the side effects declared on a <see cref="TransitionDefinition"/> that has
/// just succeeded:
/// <list type="bullet">
///   <item><see cref="TransitionDefinition.WorkItemsToCreate"/> — creates pending work items,
///   with idempotency guard (skips if an open item of the same type already exists).</item>
///   <item><see cref="TransitionDefinition.SuccessActions"/> — dispatches outbox messages for
///   recognised integration-action strings.</item>
/// </list>
/// <para>
/// Does <strong>not</strong> call <c>SaveChangesAsync</c>.  The caller that invoked
/// <see cref="ICaseTransitionService"/> owns the unit-of-work and must commit when ready.
/// </para>
/// </summary>
public interface IWorkflowSideEffectService
{
    Task ExecuteAsync(
        TransitionDefinition definition,
        SideEffectContext context,
        CancellationToken ct = default);
}
