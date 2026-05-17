using MassTransit;
using Microsoft.Extensions.Logging;
using Wfmgr.Contracts.Sagas;

namespace Wfmgr.Infrastructure.Integrations.Messaging.Sagas;

/// <summary>
/// Long-running orchestrator for the <i>contour → import</i> portion of the
/// clinical workflow. Captures durable state for "which step is this case in"
/// outside of the case row, and provides a single place to attach timeouts and
/// compensation hooks.
/// <para>
/// Lifecycle:<br/>
/// <c>Initial</c> --<see cref="StartContouringSaga"/>--&gt; <c>AwaitingContour</c><br/>
/// <c>AwaitingContour</c> --<see cref="ContourCompleted"/>--&gt; <c>AwaitingMonacoAck</c><br/>
/// <c>AwaitingMonacoAck</c> --<see cref="MonacoImportAcked"/>--&gt; final<br/>
/// Either waiting state --<see cref="ContouringSagaTimeout"/>--&gt; faulted+final.
/// </para>
/// </summary>
public sealed class ContouringSagaStateMachine
    : MassTransitStateMachine<ContouringSagaState>
{
    public State AwaitingContour { get; private set; } = null!;
    public State AwaitingMonacoAck { get; private set; } = null!;

    public Event<StartContouringSaga.V1> Started { get; private set; } = null!;
    public Event<ContourCompleted.V1> ContourDone { get; private set; } = null!;
    public Event<MonacoImportAcked.V1> ImportAcked { get; private set; } = null!;

    public ContouringSagaStateMachine(ILogger<ContouringSagaStateMachine> logger)
    {
        InstanceState(x => x.CurrentState);

        Event(() => Started, e => e.CorrelateById(ctx => ctx.Message.CaseId));
        Event(() => ContourDone, e => e.CorrelateById(ctx => ctx.Message.CaseId));
        Event(() => ImportAcked, e => e.CorrelateById(ctx => ctx.Message.CaseId));

        // NOTE: per-step timeouts (e.g. AwaitingContour > 30 min) would attach here via
        // Schedule(...) once a message scheduler is wired. Removed for now because the
        // RabbitMQ delayed_message_exchange plugin isn't part of the stock broker image.
        // The TimeoutTokenId column is reserved on the saga instance for that future use.

        Initially(
            When(Started)
                .Then(ctx =>
                {
                    ctx.Saga.AccessionNumber = ctx.Message.AccessionNumber;
                    ctx.Saga.TransitionCode = ctx.Message.TransitionCode;
                    ctx.Saga.TriggeredBy = ctx.Message.TriggeredBy;
                    ctx.Saga.StartedAt = DateTimeOffset.UtcNow;
                    logger.LogInformation(
                        "ContouringSaga started case={CaseId} accession={Accession} trigger={Trigger}",
                        ctx.Saga.CorrelationId, ctx.Saga.AccessionNumber, ctx.Saga.TriggeredBy);
                })
                .TransitionTo(AwaitingContour));

        During(AwaitingContour,
            When(ContourDone)
                .Then(ctx =>
                {
                    ctx.Saga.ContourCompletedAt = DateTimeOffset.UtcNow;
                    logger.LogInformation(
                        "ContouringSaga contour completed case={CaseId} rtstruct={RtStruct}",
                        ctx.Saga.CorrelationId, ctx.Message.RtStructSeriesInstanceUid);
                })
                .TransitionTo(AwaitingMonacoAck));

        During(AwaitingMonacoAck,
            When(ImportAcked)
                .Then(ctx =>
                {
                    ctx.Saga.MonacoAckedAt = DateTimeOffset.UtcNow;
                    ctx.Saga.CompletedAt = DateTimeOffset.UtcNow;
                    logger.LogInformation(
                        "ContouringSaga completed case={CaseId} planVersion={PlanVersion}",
                        ctx.Saga.CorrelationId, ctx.Message.PlanVersionNo);
                })
                .Finalize());

        // Once finalised, the row is removed by SetCompletedWhenFinalized so we don't keep
        // historical instances in the saga table. Compensation/audit lives in OutboxMessage
        // + CaseTransitionHistory, which are still the system of record.
        SetCompletedWhenFinalized();
    }
}
