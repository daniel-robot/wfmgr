namespace Wfmgr.Contracts.ExternalEvents;

/// <summary>
/// Bus contract for inbound external integration events (PvMed callbacks, MSQ schedule
/// events, Monaco status updates, etc.). The shape mirrors the API-layer
/// <c>ExternalIntegrationEventRequest</c> so consumers can hand the deserialized message
/// straight to <c>IExternalEventDispatcher</c>.
/// </summary>
public static class IngestExternalEvent
{
    public sealed record V1(
        MessageEnvelope Envelope,
        string Source,
        string Type,
        string ExternalId,
        Guid? CaseId,
        string? CaseAccessionNumber,
        DateTimeOffset OccurredAt,
        string? ExternalEntityType,
        string? ExternalEntityId,
        string? ExternalStatus,
        string? MetadataJson,
        string? PayloadJson,
        string? CtStudyInstanceUid,
        string? CtWadoRsUrl,
        string? RtStructSeriesInstanceUid,
        string? PlanVersionNo,
        string? FailureReason);
}
