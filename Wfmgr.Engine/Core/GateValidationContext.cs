namespace Wfmgr.Engine.Core;

/// <summary>
/// Context for gate validation — carries identity, roles, and request metadata.
/// </summary>
public class GateValidationContext
{
    public string? UserId { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
    public string? Reason { get; set; }
    public string? FormId { get; set; }
    public string? WorkItemId { get; set; }
    public string? ExternalEventPayload { get; set; }

    /// <summary>Creates a <see cref="GateValidationContext"/> from an engine <see cref="TransitionExecutionContext"/>.</summary>
    public static GateValidationContext FromTransitionContext(TransitionExecutionContext ctx) =>
        new()
        {
            UserId = ctx.TriggeredBy,
            Roles = ctx.ActorRoles,
            Reason = ctx.Reason,
        };
}
