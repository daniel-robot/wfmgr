namespace Wfmgr.Infrastructure.Persistence.Entities;

public class ExternalEventEntity
{
    public Guid EventId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? CaseCorrelationKey { get; set; }
    public Guid? CaseId { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string ProcessStatus { get; set; } = string.Empty;
    public string? Error { get; set; }

    public CaseEntity? Case { get; set; }
}
