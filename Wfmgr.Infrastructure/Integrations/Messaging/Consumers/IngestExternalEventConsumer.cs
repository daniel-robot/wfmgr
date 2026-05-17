using MassTransit;
using Microsoft.Extensions.Logging;
using Wfmgr.Application.Diagnostics;
using Wfmgr.Application.Integrations;
using Wfmgr.Application.Integrations.Dtos;
using Wfmgr.Contracts.ExternalEvents;

namespace Wfmgr.Infrastructure.Integrations.Messaging.Consumers;

/// <summary>
/// Consumes <see cref="IngestExternalEvent.V1"/> off RabbitMQ and replays it through the
/// existing <see cref="IExternalEventDispatcher"/>. The dispatcher's inbox-first dedup
/// (composite PK on <c>ExternalEventInbox</c>) handles redelivery from the broker, so
/// this consumer can rely on at-least-once semantics without special handling.
/// </summary>
public sealed class IngestExternalEventConsumer : IConsumer<IngestExternalEvent.V1>
{
    private readonly IExternalEventDispatcher _dispatcher;
    private readonly ILogger<IngestExternalEventConsumer> _logger;

    public IngestExternalEventConsumer(
        IExternalEventDispatcher dispatcher,
        ILogger<IngestExternalEventConsumer> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IngestExternalEvent.V1> context)
    {
        var msg = context.Message;
        using var activity = WfmgrActivitySource.Source.StartActivity(WfmgrActivitySource.ExternalEventReceive);
        activity?.SetTag(WfmgrActivitySource.TagExternalSource, msg.Source);
        activity?.SetTag(WfmgrActivitySource.TagExternalType, msg.Type);
        activity?.SetTag(WfmgrActivitySource.TagExternalEventId, msg.ExternalId);

        _logger.LogInformation(
            "Bus consume external event source={Source} type={Type} externalId={ExternalId} attempt={Attempt}",
            msg.Source, msg.Type, msg.ExternalId, context.GetRetryAttempt());

        var request = new ExternalIntegrationEventRequest
        {
            Source = msg.Source,
            Type = msg.Type,
            ExternalId = msg.ExternalId,
            CaseId = msg.CaseId,
            CaseAccessionNumber = msg.CaseAccessionNumber,
            OccurredAt = msg.OccurredAt,
            ExternalEntityType = msg.ExternalEntityType,
            ExternalEntityId = msg.ExternalEntityId,
            ExternalStatus = msg.ExternalStatus,
            MetadataJson = msg.MetadataJson,
            PayloadJson = msg.PayloadJson,
            CtStudyInstanceUid = msg.CtStudyInstanceUid,
            CtWadoRsUrl = msg.CtWadoRsUrl,
            RtStructSeriesInstanceUid = msg.RtStructSeriesInstanceUid,
            PlanVersionNo = msg.PlanVersionNo,
            FailureReason = msg.FailureReason,
        };

        await _dispatcher.DispatchAsync(request, context.CancellationToken);
    }
}
