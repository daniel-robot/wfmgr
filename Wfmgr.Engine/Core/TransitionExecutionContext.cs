namespace Wfmgr.Engine.Core;

/// <summary>
/// Engine-level transition execution context (status-agnostic, uses strings).
/// </summary>
public class TransitionExecutionContext
{
    public string TriggerName { get; set; } = string.Empty;
    public string TriggerType { get; set; } = "System";
    public string? TriggeredBy { get; set; }
    public IReadOnlyCollection<string> ActorRoles { get; set; } = Array.Empty<string>();
    public string? Reason { get; set; }
    public object? Metadata { get; set; }
}
