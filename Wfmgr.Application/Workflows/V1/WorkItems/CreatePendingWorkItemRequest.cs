namespace Wfmgr.Application.Workflows.V1.WorkItems;

public class CreatePendingWorkItemRequest
{
    public Guid CaseId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string AssignedRole { get; set; } = string.Empty;
    public string? AssignedUserId { get; set; }
    public int? SequenceNo { get; set; }
    public Guid? ParentWorkItemId { get; set; }
    public string? WorkItemGroup { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public int? SlaMinutes { get; set; }
    public string? ExternalCorrelationId { get; set; }
    public Guid? FormId { get; set; }
    public Guid? RequiresDifferentUserFrom { get; set; }
    public string? PayloadJson { get; set; }
    public DateTimeOffset? CreatedAtUtc { get; set; }
}
