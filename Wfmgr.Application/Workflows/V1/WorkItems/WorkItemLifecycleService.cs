using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Domain.Enums;

namespace Wfmgr.Application.Workflows.V1.WorkItems;

public class WorkItemLifecycleService : IWorkItemLifecycleService
{
    private readonly IWorkflowDataAccess _dataAccess;

    public WorkItemLifecycleService(IWorkflowDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    public async Task<WorkItemData> CreatePendingWorkItemAsync(CreatePendingWorkItemRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Type))
        {
            throw new ArgumentException("Work item type is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.AssignedRole))
        {
            throw new ArgumentException("Assigned role is required before assigning a user.", nameof(request));
        }

        var now = request.CreatedAtUtc ?? DateTimeOffset.UtcNow;
        var item = new WorkItemData
        {
            WorkItemId = Guid.NewGuid(),
            CaseId = request.CaseId,
            Type = request.Type,
            Status = WorkItemStatus.Pending,
            SequenceNo = request.SequenceNo,
            ParentWorkItemId = request.ParentWorkItemId,
            WorkItemGroup = request.WorkItemGroup,
            AssignedRole = request.AssignedRole,
            AssignedUserId = request.AssignedUserId,
            DueAt = request.DueAt,
            SlaMinutes = request.SlaMinutes,
            ExternalCorrelationId = request.ExternalCorrelationId,
            FormId = request.FormId,
            RequiresDifferentUserFrom = request.RequiresDifferentUserFrom,
            RetryCount = 0,
            PayloadJson = request.PayloadJson,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (!string.IsNullOrWhiteSpace(request.AssignedUserId))
        {
            await EnsureDifferentUserConstraintAsync(item, request.AssignedUserId, ct);
        }

        await _dataAccess.AddWorkItemAsync(item, ct);
        return item;
    }

    public void CompleteWorkItem(
        WorkItemData workItem,
        string? completedBy = null,
        string? resultCode = null,
        string? remarks = null,
        DateTimeOffset? completedAtUtc = null)
    {
        var now = completedAtUtc ?? DateTimeOffset.UtcNow;
        workItem.Status = WorkItemStatus.Done;
        workItem.ResultCode = resultCode;
        workItem.CompletedBy = completedBy;
        workItem.CompletedAt = now;
        workItem.Remarks = remarks;
        workItem.UpdatedAt = now;
    }

    public void RejectWorkItem(
        WorkItemData workItem,
        string? completedBy = null,
        string? resultCode = null,
        string? remarks = null,
        DateTimeOffset? completedAtUtc = null)
    {
        var now = completedAtUtc ?? DateTimeOffset.UtcNow;
        workItem.Status = WorkItemStatus.Rejected;
        workItem.ResultCode = string.IsNullOrWhiteSpace(resultCode) ? "REJECTED" : resultCode;
        workItem.CompletedBy = completedBy;
        workItem.CompletedAt = now;
        workItem.Remarks = remarks;
        workItem.UpdatedAt = now;
    }

    public void CancelWorkItem(
        WorkItemData workItem,
        string? completedBy = null,
        string? resultCode = null,
        string? remarks = null,
        DateTimeOffset? completedAtUtc = null)
    {
        var now = completedAtUtc ?? DateTimeOffset.UtcNow;
        workItem.Status = WorkItemStatus.Cancelled;
        workItem.ResultCode = string.IsNullOrWhiteSpace(resultCode) ? "CANCELLED" : resultCode;
        workItem.CompletedBy = completedBy;
        workItem.CompletedAt = now;
        workItem.Remarks = remarks;
        workItem.UpdatedAt = now;
    }

    public async Task AssignWorkItemAsync(WorkItemData workItem, string assignedRole, string? assignedUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(assignedRole))
        {
            throw new ArgumentException("Assigned role is required before assigning a user.", nameof(assignedRole));
        }

        if (!string.IsNullOrWhiteSpace(assignedUserId))
        {
            await EnsureDifferentUserConstraintAsync(workItem, assignedUserId, ct);
        }

        workItem.AssignedRole = assignedRole;
        workItem.AssignedUserId = assignedUserId;
        workItem.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public async Task EnsureDifferentUserConstraintAsync(WorkItemData workItem, string userId, CancellationToken ct)
    {
        if (workItem.RequiresDifferentUserFrom is null || string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var referenced = await _dataAccess.GetWorkItemByIdAsync(workItem.RequiresDifferentUserFrom.Value, ct)
            ?? throw new InvalidOperationException($"Referenced work item '{workItem.RequiresDifferentUserFrom}' was not found.");

        var sameAssignedUser = !string.IsNullOrWhiteSpace(referenced.AssignedUserId)
            && string.Equals(referenced.AssignedUserId, userId, StringComparison.OrdinalIgnoreCase);
        var sameCompletedUser = !string.IsNullOrWhiteSpace(referenced.CompletedBy)
            && string.Equals(referenced.CompletedBy, userId, StringComparison.OrdinalIgnoreCase);

        if (sameAssignedUser || sameCompletedUser)
        {
            throw new InvalidOperationException("Assigned user must be different from the referenced work item user.");
        }
    }
}
