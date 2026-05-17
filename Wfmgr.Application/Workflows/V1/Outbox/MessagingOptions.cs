namespace Wfmgr.Application.Workflows.V1.Outbox;

/// <summary>
/// Bound to the <c>Messaging</c> configuration section.
/// </summary>
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    /// <summary>
    /// Outbox action names that should be published to the message bus instead of
    /// dispatched synchronously over HTTP.
    /// </summary>
    public string[] BusActions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// When <c>true</c>, inbound webhook controllers publish each request to the bus
    /// (consumed by <c>IngestExternalEventConsumer</c>) instead of dispatching inline.
    /// Requires the broker to be configured; falls back to in-process dispatch when not.
    /// </summary>
    public bool InboundViaBus { get; set; } = false;
}
