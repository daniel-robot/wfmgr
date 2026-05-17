using Wfmgr.Domain.Enums;
using Wfmgr.Domain.Integrations;

namespace Wfmgr.Application.Abstractions.Persistence.Models;

public class OutboxMessageData
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

    public string? MessageType { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public Guid? CorrelationId { get; set; }
    public string? Traceparent { get; set; }
    public OutboxDeliveryMode DeliveryMode { get; set; } = OutboxDeliveryMode.Http;
}
