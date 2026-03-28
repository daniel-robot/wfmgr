using Wfmgr.Domain.Enums;

namespace Wfmgr.Application.Abstractions.Persistence.Models;

public class WorkItemData
{
    public Guid WorkItemId { get; set; }
    public Guid CaseId { get; set; }
    public string Type { get; set; } = string.Empty;
    public WorkItemStatus Status { get; set; }
    public string AssignedRole { get; set; } = string.Empty;
    public string? AssignedUserId { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public int? SlaMinutes { get; set; }
    public string? ExternalCorrelationId { get; set; }
    public string? PayloadJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
