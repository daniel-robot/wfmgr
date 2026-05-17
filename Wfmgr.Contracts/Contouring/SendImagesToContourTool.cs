namespace Wfmgr.Contracts.Contouring;

/// <summary>
/// Request to a contouring provider (PvMed by default; provider resolved from S1 slot) to
/// auto-contour the CT images stored against this case.
/// </summary>
public static class SendImagesToContourTool
{
    /// <summary>Version 1 contract. Append-only — do not edit fields once published.</summary>
    /// <param name="Envelope">Common message envelope.</param>
    /// <param name="CaseId">Wfmgr case identifier.</param>
    /// <param name="AccessionNumber">DICOM accession number for the CT study.</param>
    /// <param name="TransitionCode">Catalog transition code that emitted this message (e.g. <c>CON-010</c>).</param>
    /// <param name="TriggerName">Trigger name that emitted this message.</param>
    /// <param name="TriggeredBy">User or system identity that initiated the originating transition.</param>
    /// <param name="Reason">Optional human-readable reason supplied by the actor.</param>
    public sealed record V1(
        MessageEnvelope Envelope,
        Guid CaseId,
        string AccessionNumber,
        string TransitionCode,
        string TriggerName,
        string? TriggeredBy,
        string? Reason);
}
