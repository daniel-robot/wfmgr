using Microsoft.EntityFrameworkCore;
using Wfmgr.Application.Integrations;
using Wfmgr.Domain.Enums;
using Wfmgr.Infrastructure.Persistence;

namespace Wfmgr.Api.Workers;

public class OutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxWorker> _logger;

    public OutboxWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox worker batch failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        var pvMedClient = scope.ServiceProvider.GetRequiredService<IPvMedClient>();
        var monacoAdapter = scope.ServiceProvider.GetRequiredService<IMonacoAdapter>();

        var now = DateTimeOffset.UtcNow;
        var messages = await dbContext.OutboxMessages
            .Where(x =>
                (x.Status == OutboxStatus.New || x.Status == OutboxStatus.Retrying) &&
                (x.NextRetryAt == null || x.NextRetryAt <= now))
            .OrderBy(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                if (message.Action == "SEND_TO_PVMED_AUTOCONTOUR")
                {
                    await pvMedClient.SendAutoContourAsync(message.PayloadJson, ct);
                }
                else if (message.Action == "SEND_TO_MONACO_IMPORT")
                {
                    if (message.CaseId is null)
                    {
                        throw new InvalidOperationException("Outbox message CaseId is required for Monaco import.");
                    }

                    await monacoAdapter.DropImportAsync(message.CaseId.Value, message.PayloadJson, ct);
                }

                message.Status = OutboxStatus.Sent;
                message.LastTriedAt = now;
                message.NextRetryAt = null;
            }
            catch (Exception ex)
            {
                message.RetryCount += 1;
                message.Status = OutboxStatus.Retrying;
                message.LastTriedAt = now;
                var backoffMinutes = Math.Min(Math.Pow(2, message.RetryCount), 60);
                message.NextRetryAt = now.AddMinutes(backoffMinutes);

                _logger.LogError(ex, "Failed to process outbox message {MessageId}", message.MessageId);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
