namespace Wfmgr.Application.Workflows.V1.Dtos;

public class CtImageStoredRequest
{
    public string ExternalEventId { get; set; } = string.Empty;
    public string AccessionNumber { get; set; } = string.Empty;
    public DicomRef DicomRef { get; set; } = new();
    public DicomWebLocation DicomWebLocation { get; set; } = new();
    public DateTimeOffset OccurredAt { get; set; }
}
