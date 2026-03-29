using Wfmgr.Application.Abstractions.Persistence.Models;

namespace Wfmgr.Application.Workflows.V1.WorkItems;

public interface IWorkItemLifecycleService
{
    Task<WorkItemData> CreatePendingWorkItemAsync(CreatePendingWorkItemRequest request, CancellationToken ct);
    void CompleteWorkItem(WorkItemData workItem, string? completedBy = null, string? resultCode = null, string? remarks = null, DateTimeOffset? completedAtUtc = null);
    void RejectWorkItem(WorkItemData workItem, string? completedBy = null, string? resultCode = null, string? remarks = null, DateTimeOffset? completedAtUtc = null);
    void CancelWorkItem(WorkItemData workItem, string? completedBy = null, string? resultCode = null, string? remarks = null, DateTimeOffset? completedAtUtc = null);
    Task AssignWorkItemAsync(WorkItemData workItem, string assignedRole, string? assignedUserId, CancellationToken ct);
    Task EnsureDifferentUserConstraintAsync(WorkItemData workItem, string userId, CancellationToken ct);
}
