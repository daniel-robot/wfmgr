using Wfmgr.Application.Workflows.V1.StateMachine;

namespace Wfmgr.Application.Workflows.V1.Gates;

/// <summary>
/// Rich execution context passed to <see cref="IGateValidationService.ValidateAsync"/> alongside
/// a <see cref="Definitions.TransitionDefinition"/>.
/// <para>
/// Gate checks may inspect any combination of these properties to determine whether a
/// transition pre-condition is satisfied without needing to reach into the raw HTTP request.
/// </para>
/// </summary>
public sealed class GateValidationContext
{
    /// <summary>
    /// ID of the actor (user or service account) initiating the transition.
    /// <c>null</c> for fully system-initiated transitions with no human in the loop.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Roles held by <see cref="UserId"/> at the time of the request.
    /// Used by role-gated transitions; may be empty for system-triggered events.
    /// </summary>
    public IReadOnlyCollection<string> Roles { get; init; } = [];

    /// <summary>
    /// ID of a submitted form that accompanies this transition (e.g. a SimulationRecordForm).
    /// Gate checks for form-required transitions verify this is set.
    /// </summary>
    public Guid? FormId { get; init; }

    /// <summary>
    /// ID of the work item being completed as part of this transition, if any.
    /// Gate checks for task-completion transitions verify this is set.
    /// </summary>
    public Guid? WorkItemId { get; init; }

    /// <summary>
    /// Raw JSON payload of an external event trigger.
    /// Gate checks for external-event-initiated transitions verify this is non-empty.
    /// </summary>
    public string? ExternalEventPayload { get; init; }

    /// <summary>
    /// Free-text reason or rejection note provided by the actor.
    /// Required by gate checks such as <see cref="GateCheckNames.RejectionReasonRequired"/>.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Arbitrary key-value metadata for gate checks that need supplementary data.
    /// <para>Well-known keys are defined as constants on <see cref="GateCheckNames"/>
    /// (e.g. <see cref="GateCheckNames.MetaEventSource"/>,
    /// <see cref="GateCheckNames.MetaAssigneeUserId"/>).</para>
    /// </summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } =
        new Dictionary<string, object?>();

    // ── Factory helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="GateValidationContext"/> that carries only the fields available
    /// in an existing <see cref="TransitionExecutionContext"/>.
    /// </summary>
    public static GateValidationContext FromTransitionContext(TransitionExecutionContext ctx) =>
        new()
        {
            UserId = ctx.TriggeredBy,
            Roles = ctx.ActorRoles,
            Reason = ctx.Reason,
        };

    /// <summary>Returns an empty context suitable for system-only gate checks.</summary>
    public static GateValidationContext System() => new();
}
