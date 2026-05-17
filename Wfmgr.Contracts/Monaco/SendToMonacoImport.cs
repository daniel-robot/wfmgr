namespace Wfmgr.Contracts.Monaco;

/// <summary>
/// Request to Monaco TPS to import the CT study / contours for this case.
/// </summary>
public static class SendToMonacoImport
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
