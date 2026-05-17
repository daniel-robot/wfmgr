using System.Text.Json;
using System.Text.Json.Nodes;
using MassTransit;
using Microsoft.Extensions.Logging;
using Wfmgr.Application.Workflows.V1.Outbox;
using Wfmgr.Contracts;

namespace Wfmgr.Infrastructure.Integrations.Messaging;

/// <summary>
/// Publishes outbox messages onto the MassTransit bus (RabbitMQ in Phase 1).
/// </summary>
/// <remarks>
/// The outbox row stores the message payload as JSON plus a <c>MessageType</c> full-name.
/// At publish time the type is resolved via <see cref="KnownMessageTypes"/>, the JSON is
/// rehydrated into that CLR type, and the bus is asked to publish it as that type — so
/// consumers receive a strongly-typed contract.
///
/// Side-effect serialization currently writes a flat anonymous object without the
/// <see cref="MessageEnvelope"/> property defined on each contract record; this publisher
/// injects an envelope built from the outbox row's metadata before deserialization. Once
/// all enqueue sites move to serializing the typed record directly, the envelope-injection
/// branch can be removed.
/// </remarks>
public sealed class MassTransitOutboxPublisher : IOutboxPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<MassTransitOutboxPublisher> _logger;

    public MassTransitOutboxPublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<MassTransitOutboxPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public bool IsConfigured => true;

    public async Task PublishAsync(string messageType, string payloadJson, string? traceparent, CancellationToken ct)
    {
        if (!KnownMessageTypes.TryResolve(messageType, out var clrType))
        {
            throw new InvalidOperationException(
                $"Outbox message type '{messageType}' is not registered in KnownMessageTypes. " +
                "Add it to the registry or remove the action from Messaging:BusActions.");
        }

        var node = JsonNode.Parse(payloadJson)?.AsObject()
            ?? throw new InvalidOperationException(
                $"Outbox payload for '{messageType}' is not a JSON object.");

        EnsureEnvelope(node, traceparent);

        var message = node.Deserialize(clrType, JsonOptions)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize outbox payload into '{clrType.FullName}'.");

        await _publishEndpoint.Publish(message, clrType, ct);

        _logger.LogDebug(
            "Published {MessageType} to bus (traceparent={Traceparent})",
            messageType, traceparent);
    }

    private static void EnsureEnvelope(JsonObject node, string? traceparent)
    {
        // Records use PascalCase property names; serializer is case-insensitive on read.
        if (node["Envelope"] is not null || node["envelope"] is not null) return;

        node["Envelope"] = new JsonObject
        {
            ["MessageId"]   = Guid.NewGuid().ToString(),
            ["CorrelationId"] = Guid.NewGuid().ToString(),
            ["OccurredAt"]  = DateTimeOffset.UtcNow.ToString("O"),
            ["Traceparent"] = traceparent,
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };
}
