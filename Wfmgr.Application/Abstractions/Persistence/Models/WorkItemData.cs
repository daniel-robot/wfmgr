using Wfmgr.Domain.Enums;

namespace Wfmgr.Application.Abstractions.Persistence.Models;

public class WorkItemData
{
    public Guid WorkItemId { get; set; }
    public Guid CaseId { get; set; }
    public int? SequenceNo { get; set; }
    public Guid? ParentWorkItemId { get; set; }
    public string Type { get; set; } = string.Empty;
    public WorkItemStatus Status { get; set; }
    public string? WorkItemGroup { get; set; }
    public string AssignedRole { get; set; } = string.Empty;
    public string? AssignedUserId { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public int? SlaMinutes { get; set; }
    public string? ExternalCorrelationId { get; set; }
    public string? ResultCode { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    public Guid? FormId { get; set; }
    public Guid? RequiresDifferentUserFrom { get; set; }
    public int RetryCount { get; set; }
    public string? Remarks { get; set; }
    public string? PayloadJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
