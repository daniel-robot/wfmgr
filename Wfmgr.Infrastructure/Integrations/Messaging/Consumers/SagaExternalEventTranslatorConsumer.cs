using MassTransit;
using Microsoft.Extensions.Logging;
using Wfmgr.Contracts;
using Wfmgr.Contracts.ExternalEvents;
using Wfmgr.Contracts.Sagas;

namespace Wfmgr.Infrastructure.Integrations.Messaging.Consumers;

/// <summary>
/// Translates inbound <see cref="IngestExternalEvent.V1"/> messages into typed saga
/// events when the source/type combination matches a step the saga listens for.
/// Runs alongside <see cref="IngestExternalEventConsumer"/> (different endpoint),
/// so the dispatcher still receives the event for the in-process flow.
/// </summary>
public sealed class SagaExternalEventTranslatorConsumer : IConsumer<IngestExternalEvent.V1>
{
    // Source/type tokens that map to saga events. Kept liberal so production wiring
    // can match either the contouring provider's native vocabulary or the wfmgr
    // normalised vocabulary.
    private static readonly string[] ContourCompletedTypes =
        { "contour.completed", "contouring.completed", "pvmed.contour.completed" };

    private static readonly string[] MonacoAckTypes =
        { "monaco.import.acked", "monaco.import.acknowledged", "tps.plan.received" };

    private readonly IPublishEndpoint _publish;
    private readonly ILogger<SagaExternalEventTranslatorConsumer> _logger;

    public SagaExternalEventTranslatorConsumer(
        IPublishEndpoint publish,
        ILogger<SagaExternalEventTranslatorConsumer> logger)
    {
        _publish = publish;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IngestExternalEvent.V1> context)
    {
        var msg = context.Message;
        if (msg.CaseId is null)
        {
            return; // saga is correlated by CaseId; nothing to do
        }
        var caseId = msg.CaseId.Value;
        var envelope = new MessageEnvelope(
            MessageId: Guid.NewGuid(),
            CorrelationId: caseId,
            OccurredAt: msg.OccurredAt == default ? DateTimeOffset.UtcNow : msg.OccurredAt,
            Traceparent: msg.Envelope.Traceparent);

        if (Matches(msg.Type, ContourCompletedTypes))
        {
            _logger.LogInformation(
                "Translating external event {Source}/{Type} -> ContourCompleted case={CaseId}",
                msg.Source, msg.Type, caseId);
            await _publish.Publish(
                new ContourCompleted.V1(envelope, caseId, msg.CaseAccessionNumber ?? string.Empty, msg.RtStructSeriesInstanceUid),
                context.CancellationToken);
            return;
        }

        if (Matches(msg.Type, MonacoAckTypes))
        {
            _logger.LogInformation(
                "Translating external event {Source}/{Type} -> MonacoImportAcked case={CaseId}",
                msg.Source, msg.Type, caseId);
            await _publish.Publish(
                new MonacoImportAcked.V1(envelope, caseId, msg.CaseAccessionNumber ?? string.Empty, msg.PlanVersionNo),
                context.CancellationToken);
        }
    }

    private static bool Matches(string? type, string[] candidates)
    {
        if (string.IsNullOrWhiteSpace(type)) return false;
        foreach (var c in candidates)
        {
            if (string.Equals(type, c, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
