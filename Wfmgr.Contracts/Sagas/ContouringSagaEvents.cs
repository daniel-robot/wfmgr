namespace Wfmgr.Contracts.Sagas;

/// <summary>
/// Starts a new <c>ContouringSaga</c> instance for a case entering the contour →
/// import flow. Published by the saga relay consumer when a
/// <see cref="Wfmgr.Contracts.Contouring.SendImagesToContourTool.V1"/> message is
/// observed on the bus, so the transition pipeline does not need a direct
/// dependency on saga contracts.
/// </summary>
public static class StartContouringSaga
{
    public sealed record V1(
        MessageEnvelope Envelope,
        Guid CaseId,
        string AccessionNumber,
        string TransitionCode,
        string? TriggeredBy);
}

/// <summary>
/// Signals that the contouring provider has finished and contours are available
/// for import. Translated from an inbound <c>IngestExternalEvent.V1</c> whose
/// <c>Type</c> indicates contour completion.
/// </summary>
public static class ContourCompleted
{
    public sealed record V1(
        MessageEnvelope Envelope,
        Guid CaseId,
        string AccessionNumber,
        string? RtStructSeriesInstanceUid);
}

/// <summary>
/// Signals that Monaco has acknowledged the import request and the plan is now
/// being prepared on the TPS side. Translated from an inbound external event.
/// </summary>
public static class MonacoImportAcked
{
    public sealed record V1(
        MessageEnvelope Envelope,
        Guid CaseId,
        string AccessionNumber,
        string? PlanVersionNo);
}

/// <summary>
/// Internal schedule message used by the saga to escalate a stuck step. Not
/// intended to be published by application code.
/// </summary>
public sealed record ContouringSagaTimeout(Guid CaseId, string Step);
