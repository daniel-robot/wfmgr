using Wfmgr.Domain.Enums;

namespace Wfmgr.Application.Workflows.V1.StateMachine;

public class TransitionExecutionContext
{
    public string TriggerName { get; set; } = string.Empty;
    public WorkflowTriggerType TriggerType { get; set; } = WorkflowTriggerType.System;
    public string? TriggeredBy { get; set; }
    public IReadOnlyCollection<string> ActorRoles { get; set; } = Array.Empty<string>();
    public string? Reason { get; set; }
    public object? Metadata { get; set; }
}
