using MassTransit;
using Microsoft.Extensions.Logging;
using Wfmgr.Application.Integrations;
using Wfmgr.Contracts.Monaco;

namespace Wfmgr.Infrastructure.Integrations.Messaging.Consumers;

/// <summary>
/// In-process MassTransit consumer for <see cref="SendToMonacoImport.V1"/>.
/// Acts as the bus counterpart to the legacy synchronous HTTP dispatch path: receives the
/// typed contract from RabbitMQ and hands it to the existing <see cref="IMonacoAdapter"/>
/// implementation so Phase 1 reuses the established Monaco integration code unchanged.
/// </summary>
public sealed class SendToMonacoImportConsumer : IConsumer<SendToMonacoImport.V1>
{
    private readonly IMonacoAdapter _monaco;
    private readonly ILogger<SendToMonacoImportConsumer> _logger;

    public SendToMonacoImportConsumer(
        IMonacoAdapter monaco,
        ILogger<SendToMonacoImportConsumer> logger)
    {
        _monaco = monaco;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SendToMonacoImport.V1> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Bus consume {MessageType} case={CaseId} accession={Accession} attempt={Attempt}",
            nameof(SendToMonacoImport.V1), msg.CaseId, msg.AccessionNumber,
            context.GetRetryAttempt());

        // Pass-through to existing adapter. Adapter throws on failure → MassTransit retries.
        await _monaco.SendToMonacoImportAsync(msg.CaseId, System.Text.Json.JsonSerializer.Serialize(msg), context.CancellationToken);
    }
}
