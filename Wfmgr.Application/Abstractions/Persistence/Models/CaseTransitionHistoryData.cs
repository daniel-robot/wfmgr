namespace Wfmgr.Application.Abstractions.Persistence.Models;

public class CaseTransitionHistoryData
{
    public Guid TransitionId { get; set; }
    public Guid CaseId { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public string TriggerName { get; set; } = string.Empty;
    public string? TriggeredBy { get; set; }
    public string? Reason { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
