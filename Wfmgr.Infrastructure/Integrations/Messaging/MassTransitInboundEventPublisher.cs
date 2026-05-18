using MassTransit;
using Microsoft.Extensions.Logging;
using Wfmgr.Application.Diagnostics;
using Wfmgr.Application.Workflows.V1.Inbound;
using Wfmgr.Contracts.ExternalEvents;

namespace Wfmgr.Infrastructure.Integrations.Messaging;

public sealed class MassTransitInboundEventPublisher : IInboundEventPublisher
{
    private readonly IPublishEndpoint _publish;
    private readonly ILogger<MassTransitInboundEventPublisher> _logger;

    public MassTransitInboundEventPublisher(
        IPublishEndpoint publish,
        ILogger<MassTransitInboundEventPublisher> logger)
    {
        _publish = publish;
        _logger = logger;
    }

    public bool IsConfigured => true;

    public async Task PublishAsync(IngestExternalEvent.V1 message, CancellationToken ct)
    {
        await _publish.Publish(message, ct);
        _logger.LogDebug(
            "Published inbound external event source={Source} type={Type} externalId={ExternalId} traceparent={Traceparent}",
            message.Source, message.Type, message.ExternalId, message.Envelope.Traceparent);
    }
}
