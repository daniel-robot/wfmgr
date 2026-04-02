using Microsoft.EntityFrameworkCore;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Integrations;
using Wfmgr.Application.Workflows.V1.Compensation;
using Wfmgr.Domain.Enums;
using Wfmgr.Domain.Integrations;
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

    private const int CompensationRetryThreshold = 5;

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        var pvMedClient = scope.ServiceProvider.GetRequiredService<IPvMedClient>();
        var monacoAdapter = scope.ServiceProvider.GetRequiredService<IMonacoAdapter>();
        var msqClient = scope.ServiceProvider.GetRequiredService<IMsqClient>();
        var dataAccess = scope.ServiceProvider.GetRequiredService<IWorkflowDataAccess>();
        var compensation = scope.ServiceProvider.GetRequiredService<IWorkflowCompensationService>();

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
                if (message.Action == OutboxActions.SendImagesToContourTool || message.Action == "SEND_TO_PVMED_AUTOCONTOUR")
                {
                    await pvMedClient.SendAutoContourAsync(message.PayloadJson, ct);
                }
                else if (message.Action == OutboxActions.SendToMonacoImport || message.Action == "SEND_TO_MONACO_IMPORT")
                {
                    if (message.CaseId is null)
                    {
                        throw new InvalidOperationException("Outbox message CaseId is required for Monaco import.");
                    }

                    await monacoAdapter.SendToMonacoImportAsync(message.CaseId.Value, message.PayloadJson, ct);
                }
                else if (message.Action == OutboxActions.QueryContourStatus)
                {
                    await pvMedClient.QueryContourStatusAsync(message.PayloadJson, ct);
                }
                else if (message.Action == OutboxActions.GeneratePrescription)
                {
                    await msqClient.GeneratePrescriptionAsync(message.PayloadJson, ct);
                }
                else if (message.Action == OutboxActions.SyncSchedule)
                {
                    await msqClient.SyncScheduleAsync(message.PayloadJson, ct);
                }
                else if (message.Action == OutboxActions.QueryTreatmentProgress)
                {
                    await msqClient.QueryTreatmentProgressAsync(message.PayloadJson, ct);
                }
                else
                {
                    throw new InvalidOperationException($"Unknown outbox action '{message.Action}'.");
                }

                message.Status = OutboxStatus.Sent;
                message.LastTriedAt = now;
                message.NextRetryAt = null;
            }
            catch (Exception ex)
            {
                message.RetryCount += 1;
                message.LastTriedAt = now;

                if (message.RetryCount >= CompensationRetryThreshold && message.CaseId is not null)
                {
                    // Retry budget exhausted — escalate to compensation service.
                    message.Status = OutboxStatus.Failed;
                    message.NextRetryAt = null;

                    var (failedStepCode, failureNote) = message.Action switch
                    {
                        OutboxActions.SendImagesToContourTool =>
                            ("IMG-002", "Outbox send to contouring tool exhausted retries"),
                        OutboxActions.SendToMonacoImport =>
                            ("IMG-002", "Outbox send to Monaco import exhausted retries"),
                        OutboxActions.SyncSchedule =>
                            ("TRT-001", "Outbox schedule sync exhausted retries"),
                        OutboxActions.GeneratePrescription =>
                            ("RX-006", "Outbox prescription generation exhausted retries"),
                        _ => (string.Empty, string.Empty)
                    };

                    if (!string.IsNullOrEmpty(failedStepCode))
                    {
                        try
                        {
                            var compResult = await compensation.HandleFailureAsync(
                                message.CaseId.Value,
                                failedStepCode,
                                new CompensationContext
                                {
                                    Reason = $"{failureNote}: {ex.Message}",
                                    SourceSystem = message.TargetSystem,
                                    FailedOutboxMessageId = message.MessageId,
                                    RetryCount = message.RetryCount,
                                },
                                ct);

                            if (compResult.IsSuccess)
                            {
                                await dataAccess.SaveChangesAsync(ct);
                                _logger.LogWarning(
                                    "Outbox message {MessageId} action {Action} failed after {RetryCount} retries. " +
                                    "Compensation {CompCode} applied: {Summary}",
                                    message.MessageId, message.Action, message.RetryCount,
                                    compResult.CompensationCode, compResult.ToSummary());
                            }
                            else
                            {
                                _logger.LogError(
                                    "Outbox message {MessageId} action {Action} failed after {RetryCount} retries " +
                                    "and compensation also failed: {Detail}",
                                    message.MessageId, message.Action, message.RetryCount,
                                    compResult.FailureDetail);
                            }
                        }
                        catch (Exception compEx)
                        {
                            _logger.LogError(compEx,
                                "Compensation for outbox message {MessageId} threw an exception.",
                                message.MessageId);
                        }
                    }
                }
                else
                {
                    message.Status = OutboxStatus.Retrying;
                    var backoffMinutes = Math.Min(Math.Pow(2, message.RetryCount), 60);
                    message.NextRetryAt = now.AddMinutes(backoffMinutes);
                }

                _logger.LogError(ex, "Failed to process outbox message {MessageId} (attempt {RetryCount})",
                    message.MessageId, message.RetryCount);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
