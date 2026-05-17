using MassTransit;

namespace Wfmgr.Infrastructure.Integrations.Messaging.Sagas;

/// <summary>
/// Durable instance row for a single <c>ContouringSaga</c>. Correlated by
/// <see cref="CorrelationId"/> which is set to the case id at saga start; this
/// guarantees that any subsequent event for the same case (contour-complete,
/// import-ack, timeout) lands on the existing instance.
/// </summary>
public sealed class ContouringSagaState : SagaStateMachineInstance, ISagaVersion
{
    /// <summary>Case id. Doubles as the correlation id.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Optimistic-concurrency token managed by MassTransit's EF repository.</summary>
    public int Version { get; set; }

    /// <summary>State name set by MassTransit (matches <c>State.Name</c>).</summary>
    public string CurrentState { get; set; } = string.Empty;

    public string AccessionNumber { get; set; } = string.Empty;
    public string? TransitionCode { get; set; }
    public string? TriggeredBy { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? ContourCompletedAt { get; set; }
    public DateTimeOffset? MonacoAckedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? FaultReason { get; set; }

    /// <summary>Scheduled-timeout token. Persisted so the saga can cancel an in-flight timeout.</summary>
    public Guid? TimeoutTokenId { get; set; }
}
