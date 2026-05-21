using Wfmgr.Engine.Abstractions;

namespace Wfmgr.Engine.Core;

/// <summary>
/// Context passed to side-effect handlers after a successful transition.
/// </summary>
public class SideEffectContext
{
    public IWorkflowSubject Subject { get; set; } = null!;
    public GateValidationContext ValidationContext { get; set; } = null!;
    public TransitionDefinition? Transition { get; set; }
    public DateTimeOffset Now { get; set; }
}
