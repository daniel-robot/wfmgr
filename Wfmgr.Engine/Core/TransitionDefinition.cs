namespace Wfmgr.Engine.Core;

/// <summary>
/// Engine-level transition definition using string status identifiers.
/// The host maps its domain status enums to/from strings at the boundary.
/// </summary>
public sealed class TransitionDefinition
{
    public required string Code { get; init; }
    public required string[] FromStatuses { get; init; }
    public required string ToStatus { get; init; }
    public required string TriggerName { get; init; }
    public string TriggerType { get; init; } = "System";
    public IReadOnlyList<string> RequiredRoles { get; init; } = [];
    public string[] GateChecks { get; init; } = [];
    public string[] SuccessActions { get; init; } = [];
    public string[] FailureActions { get; init; } = [];
    public string[] WorkItemsToCreate { get; init; } = [];
    public string? ConfigSlot { get; init; }
}
