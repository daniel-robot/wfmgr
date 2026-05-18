namespace Wfmgr.Contracts.Scheduling;

/// <summary>
/// Request to MSQ to (re)sync the treatment schedule for this case.
/// </summary>
public static class SyncSchedule
{
    /// <summary>Version 1 contract. Append-only — do not edit fields once published.</summary>
    public sealed record V1(
        MessageEnvelope Envelope,
        Guid CaseId,
        string AccessionNumber,
        string TransitionCode,
        string TriggerName,
        string? TriggeredBy,
        string? Reason);
}
