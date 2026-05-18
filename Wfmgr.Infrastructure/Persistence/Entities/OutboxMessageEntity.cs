using Wfmgr.Domain.Enums;
using Wfmgr.Domain.Integrations;

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

    /// <summary>Typed contract FQN, e.g. <c>Wfmgr.Contracts.Monaco.SendToMonacoImport+V1</c>. Null for legacy rows.</summary>
    public string? MessageType { get; set; }

    /// <summary>Wire-format version of the message payload. Starts at 1; bumped when a non-backward-compatible field is added.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Logical correlation key across a chain of related messages. Typically the case id.</summary>
    public Guid? CorrelationId { get; set; }

    /// <summary>W3C trace-context <c>traceparent</c> captured at enqueue time so the broker hop is traceable.</summary>
    public string? Traceparent { get; set; }

    /// <summary>Transport the worker should use when dispatching this message.</summary>
    public OutboxDeliveryMode DeliveryMode { get; set; } = OutboxDeliveryMode.Http;

    public CaseEntity? Case { get; set; }
}
