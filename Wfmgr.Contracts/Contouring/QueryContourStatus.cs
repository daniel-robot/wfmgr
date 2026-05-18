namespace Wfmgr.Contracts.Contouring;

/// <summary>
/// Poll the contouring provider for the current state of an in-flight auto-contour job.
/// </summary>
public static class QueryContourStatus
{
    /// <summary>Version 1 contract. Append-only — do not edit fields once published.</summary>
    public sealed record V1(
        MessageEnvelope Envelope,
        Guid CaseId,
        string AccessionNumber,
        string TransitionCode,
        string TriggerName);
}
