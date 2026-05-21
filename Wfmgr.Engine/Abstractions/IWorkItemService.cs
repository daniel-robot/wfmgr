namespace Wfmgr.Engine.Abstractions;

/// <summary>
/// Manages work items for workflow subjects.
/// The host provides its own implementation.
/// </summary>
public interface IWorkItemService
{
    Task<string> CreatePendingWorkItemAsync(CreateWorkItemRequest request, CancellationToken ct);
    Task CompleteWorkItemAsync(string workItemId, string completedBy, string resultCode, DateTimeOffset completedAtUtc, CancellationToken ct);
    Task RejectWorkItemAsync(string workItemId, string completedBy, string resultCode, string? remarks, DateTimeOffset completedAtUtc, CancellationToken ct);
    Task CancelWorkItemAsync(string workItemId, string completedBy, string resultCode, string? remarks, DateTimeOffset completedAtUtc, CancellationToken ct);
}

/// <summary>
/// Engine-level request for creating a pending work item.
/// </summary>
public class CreateWorkItemRequest
{
    public string SubjectId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string AssignedRole { get; set; } = string.Empty;
    public int? SlaMinutes { get; set; }
    public string? ExternalCorrelationId { get; set; }
    public string? PayloadJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
