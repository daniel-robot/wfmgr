namespace Wfmgr.Contracts;

/// <summary>
/// Common fields carried on every wfmgr message contract.
/// <para>
/// Contracts compose this record by including its fields rather than inheriting from it,
/// so each message remains a self-contained DTO that can be serialised with any payload
/// shape (System.Text.Json, MessagePack, protobuf, …).
/// </para>
/// </summary>
/// <param name="MessageId">Globally unique id for this delivery attempt. Used for inbox dedup.</param>
/// <param name="CorrelationId">Logical correlation key across a chain of related messages (typically the case id).</param>
/// <param name="OccurredAt">UTC timestamp the source-of-truth event occurred (not necessarily when the message was emitted).</param>
/// <param name="Traceparent">W3C trace-context <c>traceparent</c> header value to preserve distributed tracing across the broker.</param>
public sealed record MessageEnvelope(
    Guid MessageId,
    Guid CorrelationId,
    DateTimeOffset OccurredAt,
    string? Traceparent);
