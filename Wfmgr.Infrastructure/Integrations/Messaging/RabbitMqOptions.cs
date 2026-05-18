namespace Wfmgr.Infrastructure.Integrations.Messaging;

/// <summary>
/// Configuration for the RabbitMQ host. When <see cref="Host"/> is null or whitespace,
/// MassTransit is not registered and <see cref="NoOpOutboxPublisher"/> is used instead;
/// any outbox row stamped with <c>DeliveryMode = Bus</c> will then fail fast — surfacing
/// misconfiguration immediately rather than silently dropping messages.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string? Host { get; set; }
    public string VirtualHost { get; set; } = "/";
    public ushort Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";

    /// <summary>
    /// HTTP port of the RabbitMQ management plugin. Used by the messaging health
    /// check to scrape <c>*_error</c> queue depths. Defaults to the standard 15672.
    /// </summary>
    public ushort ManagementPort { get; set; } = 15672;

    /// <summary>
    /// Scheme for the management API (<c>http</c> for local dev, <c>https</c> in prod
    /// behind TLS termination).
    /// </summary>
    public string ManagementScheme { get; set; } = "http";

    /// <summary>
    /// DLQ depth (across all <c>*_error</c> queues) at which the messaging health check
    /// reports Degraded. Setting to 0 disables the threshold check.
    /// </summary>
    public int DeadLetterDegradedThreshold { get; set; } = 1;

    /// <summary>
    /// DLQ depth at which the health check reports Unhealthy. Setting to 0 disables.
    /// </summary>
    public int DeadLetterUnhealthyThreshold { get; set; } = 100;
}
