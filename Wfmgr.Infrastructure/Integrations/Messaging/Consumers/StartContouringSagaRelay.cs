using MassTransit;
using Microsoft.Extensions.Logging;
using Wfmgr.Application.Diagnostics;
using Wfmgr.Contracts;
using Wfmgr.Contracts.Contouring;
using Wfmgr.Contracts.Sagas;

namespace Wfmgr.Infrastructure.Integrations.Messaging.Consumers;

/// <summary>
/// Listens to <see cref="SendImagesToContourTool.V1"/> on the bus and publishes a
/// matching <see cref="StartContouringSaga.V1"/> to kick off the ContouringSaga.
/// Keeps the transition pipeline ignorant of saga contracts — the existing outbox
/// row that emits the contour-tool message is sufficient to start the saga.
/// </summary>
public sealed class StartContouringSagaRelay : IConsumer<SendImagesToContourTool.V1>
{
    private readonly IPublishEndpoint _publish;
    private readonly ILogger<StartContouringSagaRelay> _logger;

    public StartContouringSagaRelay(
        IPublishEndpoint publish,
        ILogger<StartContouringSagaRelay> logger)
    {
        _publish = publish;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SendImagesToContourTool.V1> context)
    {
        var msg = context.Message;
        var traceparent = WfmgrActivitySource.CurrentTraceparent() ?? msg.Envelope.Traceparent;

        var start = new StartContouringSaga.V1(
            Envelope: new MessageEnvelope(
                MessageId: Guid.NewGuid(),
                CorrelationId: msg.CaseId,
                OccurredAt: DateTimeOffset.UtcNow,
                Traceparent: traceparent),
            CaseId: msg.CaseId,
            AccessionNumber: msg.AccessionNumber,
            TransitionCode: msg.TransitionCode,
            TriggeredBy: msg.TriggeredBy);

        _logger.LogInformation(
            "Relaying StartContouringSaga for case={CaseId} accession={Accession}",
            msg.CaseId, msg.AccessionNumber);

        await _publish.Publish(start, context.CancellationToken);
    }
}
