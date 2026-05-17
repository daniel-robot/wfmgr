namespace Wfmgr.Contracts.Monaco;

/// <summary>
/// Request to Monaco / MSQ to report progress for an in-flight treatment.
/// </summary>
public static class QueryTreatmentProgress
{
    /// <summary>Version 1 contract. Append-only — do not edit fields once published.</summary>
    public sealed record V1(
        MessageEnvelope Envelope,
        Guid CaseId,
        string AccessionNumber,
        string TransitionCode,
        string TriggerName);
}
