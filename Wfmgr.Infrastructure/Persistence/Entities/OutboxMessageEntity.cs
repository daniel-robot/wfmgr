using Wfmgr.Domain.Enums;

namespace Wfmgr.Infrastructure.Persistence.Entities;

public class OutboxMessageEntity
{
    public Guid MessageId { get; set; }
    public Guid? CaseId { get; set; }
    public string TargetSystem { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public OutboxStatus Status { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastTriedAt { get; set; }

    public CaseEntity? Case { get; set; }
}
