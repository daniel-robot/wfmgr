namespace Wfmgr.Contracts.Prescription;

/// <summary>
/// Request to the oncology / RX system to generate or sync the prescription for this case.
/// </summary>
public static class GeneratePrescription
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
