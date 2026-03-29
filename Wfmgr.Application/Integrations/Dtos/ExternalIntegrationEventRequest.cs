namespace Wfmgr.Application.Integrations.Dtos;

public class ExternalIntegrationEventRequest
{
    public string Source { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public Guid? CaseId { get; set; }
    public string? CaseAccessionNumber { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    public string? ExternalEntityType { get; set; }
    public string? ExternalEntityId { get; set; }
    public string? ExternalStatus { get; set; }
    public string? MetadataJson { get; set; }
    public string? PayloadJson { get; set; }

    public string? CtStudyInstanceUid { get; set; }
    public string? CtWadoRsUrl { get; set; }
    public string? RtStructSeriesInstanceUid { get; set; }
    public string? PlanVersionNo { get; set; }
    public string? FailureReason { get; set; }
}
