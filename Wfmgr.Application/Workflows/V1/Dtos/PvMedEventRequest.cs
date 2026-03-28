namespace Wfmgr.Application.Workflows.V1.Dtos;

public class PvMedEventRequest
{
    public string ExternalEventId { get; set; } = string.Empty;
    public Guid CaseId { get; set; }
    public string Type { get; set; } = string.Empty;
    public PvMedJob PvMedJob { get; set; } = new();
    public PvMedResult? PvMedResult { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
