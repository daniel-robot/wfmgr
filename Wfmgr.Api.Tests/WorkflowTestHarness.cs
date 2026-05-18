using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wfmgr.Domain.Enums;
using Wfmgr.Infrastructure.Persistence;

namespace Wfmgr.Api.Tests;

/// <summary>
/// Thin read-only projections over <see cref="WfmgrDbContext"/> for assertion-style tests.
/// Phase 0 shim: lets new tests assert outbox / transition / work-item state without
/// repeating raw DbContext queries. Existing tests are not migrated in bulk; adopt
/// piecemeal when writing new tests.
/// </summary>
public sealed class WorkflowTestHarness
{
    private readonly IServiceProvider _root;

    public WorkflowTestHarness(IServiceProvider root)
    {
        _root = root;
    }

    public IReadOnlyList<OutboxDispatchView> OutboxDispatched(string? action = null, string? target = null)
    {
        using var scope = _root.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        var q = db.OutboxMessages.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(action)) q = q.Where(x => x.Action == action);
        if (!string.IsNullOrEmpty(target)) q = q.Where(x => x.TargetSystem == target);
        return q.OrderBy(x => x.CreatedAt)
            .Select(x => new OutboxDispatchView(
                x.MessageId, x.CaseId, x.TargetSystem, x.Action,
                x.MessageType, x.Status, x.RetryCount, x.DeliveryMode, x.CorrelationId))
            .ToList();
    }

    public IReadOnlyList<TransitionView> RecordedTransitions(string? triggerName = null, string? from = null, string? to = null)
    {
        using var scope = _root.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        var q = db.CaseTransitionHistories.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(triggerName)) q = q.Where(x => x.TriggerName == triggerName);
        if (!string.IsNullOrEmpty(from)) q = q.Where(x => x.FromStatus == from);
        if (!string.IsNullOrEmpty(to)) q = q.Where(x => x.ToStatus == to);
        return q.OrderBy(x => x.CreatedAt)
            .Select(x => new TransitionView(x.CaseId, x.TriggerName, x.FromStatus, x.ToStatus, x.CreatedAt))
            .ToList();
    }

    public IReadOnlyList<WorkItemView> WorkItemsCreated(string? type = null)
    {
        using var scope = _root.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        var q = db.WorkItems.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(type)) q = q.Where(x => x.Type == type);
        return q.OrderBy(x => x.SequenceNo)
            .Select(x => new WorkItemView(x.WorkItemId, x.CaseId, x.Type, x.Status, x.AssignedRole))
            .ToList();
    }

    public IReadOnlyList<ExternalEventInboxView> InboxEntries(string? integration = null)
    {
        using var scope = _root.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        var q = db.ExternalEventInbox.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(integration)) q = q.Where(x => x.Integration == integration);
        return q.OrderBy(x => x.ReceivedAt)
            .Select(x => new ExternalEventInboxView(x.Integration, x.ExternalEventId, x.CaseId, x.ProcessedAt))
            .ToList();
    }
}

public sealed record OutboxDispatchView(
    Guid MessageId, Guid? CaseId, string TargetSystem, string Action,
    string? MessageType, OutboxStatus Status, int RetryCount,
    Wfmgr.Domain.Integrations.OutboxDeliveryMode DeliveryMode, Guid? CorrelationId);

public sealed record TransitionView(Guid CaseId, string TriggerName, string? FromStatus, string ToStatus, DateTimeOffset CreatedAt);

public sealed record WorkItemView(Guid WorkItemId, Guid CaseId, string Type, WorkItemStatus Status, string AssignedRole);

public sealed record ExternalEventInboxView(string Integration, string ExternalEventId, Guid? CaseId, DateTimeOffset? ProcessedAt);
