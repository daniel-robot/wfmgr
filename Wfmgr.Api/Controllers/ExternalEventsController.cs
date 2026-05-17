using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wfmgr.Application.Diagnostics;
using Wfmgr.Application.Integrations;
using Wfmgr.Application.Integrations.Dtos;
using Wfmgr.Application.Workflows.V1.Inbound;
using Wfmgr.Application.Workflows.V1.Outbox;
using Wfmgr.Contracts;
using Wfmgr.Contracts.ExternalEvents;

namespace Wfmgr.Api.Controllers;

[ApiController]
[Route("api/integration/events")]
// TODO: Add API key or mTLS authentication for external system callbacks before production.
public class ExternalEventsController : ControllerBase
{
    private readonly IExternalEventDispatcher _dispatcher;
    private readonly IInboundEventPublisher _publisher;
    private readonly MessagingOptions _messaging;

    public ExternalEventsController(
        IExternalEventDispatcher dispatcher,
        IInboundEventPublisher publisher,
        IOptions<MessagingOptions> messaging)
    {
        _dispatcher = dispatcher;
        _publisher = publisher;
        _messaging = messaging.Value;
    }

    [HttpPost]
    public async Task<IActionResult> Dispatch([FromBody] ExternalIntegrationEventRequest request, CancellationToken ct)
    {
        if (_messaging.InboundViaBus && _publisher.IsConfigured)
        {
            // Bus path: publish and return 202 immediately. The consumer will replay this
            // through IExternalEventDispatcher; the inbox table dedups any redelivery.
            await _publisher.PublishAsync(ToContract(request), ct);
            return Accepted();
        }

        // In-process path (default): dispatch synchronously.
        await _dispatcher.DispatchAsync(request, ct);
        return Accepted();
    }

    private static IngestExternalEvent.V1 ToContract(ExternalIntegrationEventRequest r) =>
        new(
            Envelope: new MessageEnvelope(
                MessageId: Guid.NewGuid(),
                CorrelationId: r.CaseId ?? Guid.NewGuid(),
                OccurredAt: r.OccurredAt == default ? DateTimeOffset.UtcNow : r.OccurredAt,
                Traceparent: WfmgrActivitySource.CurrentTraceparent()),
            Source: r.Source,
            Type: r.Type,
            ExternalId: r.ExternalId,
            CaseId: r.CaseId,
            CaseAccessionNumber: r.CaseAccessionNumber,
            OccurredAt: r.OccurredAt,
            ExternalEntityType: r.ExternalEntityType,
            ExternalEntityId: r.ExternalEntityId,
            ExternalStatus: r.ExternalStatus,
            MetadataJson: r.MetadataJson,
            PayloadJson: r.PayloadJson,
            CtStudyInstanceUid: r.CtStudyInstanceUid,
            CtWadoRsUrl: r.CtWadoRsUrl,
            RtStructSeriesInstanceUid: r.RtStructSeriesInstanceUid,
            PlanVersionNo: r.PlanVersionNo,
            FailureReason: r.FailureReason);
}
