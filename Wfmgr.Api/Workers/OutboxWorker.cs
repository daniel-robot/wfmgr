using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Diagnostics;
using Wfmgr.Application.Integrations;
using Wfmgr.Application.Workflows.V1.Compensation;
using Wfmgr.Application.Workflows.V1.Outbox;
using Wfmgr.Domain.Enums;
using Wfmgr.Domain.Integrations;
using Wfmgr.Infrastructure.Persistence;
using Wfmgr.Infrastructure.Persistence.Entities;

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
        var msqClient = scope.ServiceProvider.GetRequiredService<IMsqClient>();
        var dataAccess = scope.ServiceProvider.GetRequiredService<IWorkflowDataAccess>();
        var compensation = scope.ServiceProvider.GetRequiredService<IWorkflowCompensationService>();
        var publisher = scope.ServiceProvider.GetRequiredService<IOutboxPublisher>();

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
            await TryDeliverAsync(message, now, pvMedClient, monacoAdapter, msqClient, publisher, dataAccess, compensation, ct);
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private async Task TryDeliverAsync(
        OutboxMessageEntity message,
        DateTimeOffset now,
        IPvMedClient pvMedClient,
        IMonacoAdapter monacoAdapter,
        IMsqClient msqClient,
        IOutboxPublisher publisher,
        IWorkflowDataAccess dataAccess,
        IWorkflowCompensationService compensation,
        CancellationToken ct)
    {
        using var activity = StartDeliveryActivity(message);
        var policy = OutboxRetryPolicyMap.Get(message.Action);

        try
        {
            switch (message.DeliveryMode)
            {
                case OutboxDeliveryMode.Bus:
                    if (string.IsNullOrEmpty(message.MessageType))
                        throw new InvalidOperationException(
                            $"Outbox message {message.MessageId} has DeliveryMode=Bus but no MessageType.");
                    await publisher.PublishAsync(
                        message.MessageType, message.PayloadJson, message.Traceparent, ct);
                    break;

                case OutboxDeliveryMode.Http:
                default:
                    await DispatchHttpAsync(message, pvMedClient, monacoAdapter, msqClient, ct);
                    break;
            }

            message.Status = OutboxStatus.Sent;
            message.LastTriedAt = now;
            message.NextRetryAt = null;
            activity?.SetTag(WfmgrActivitySource.TagResult, "sent");
        }
        catch (Exception ex)
        {
            HandleFailure(message, ex, now, policy);
            activity?.SetTag(WfmgrActivitySource.TagResult, "failed");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            if (message.Status == OutboxStatus.Failed && message.CaseId is not null)
            {
                await EscalateToCompensationAsync(message, ex, dataAccess, compensation, ct);
            }
        }
    }

    private static Activity? StartDeliveryActivity(OutboxMessageEntity message)
    {
        var activity = WfmgrActivitySource.Source.StartActivity(WfmgrActivitySource.OutboxDeliver);
        activity?.SetTag(WfmgrActivitySource.TagCaseId, message.CaseId);
        activity?.SetTag(WfmgrActivitySource.TagOutboxAction, message.Action);
        activity?.SetTag(WfmgrActivitySource.TagOutboxTarget, message.TargetSystem);
        activity?.SetTag(WfmgrActivitySource.TagOutboxDeliveryMode, message.DeliveryMode.ToString());
        activity?.SetTag(WfmgrActivitySource.TagOutboxAttempt, message.RetryCount + 1);
        activity?.SetTag(WfmgrActivitySource.TagOutboxMessageType, message.MessageType);
        return activity;
    }

    private static async Task DispatchHttpAsync(
        OutboxMessageEntity message,
        IPvMedClient pvMedClient,
        IMonacoAdapter monacoAdapter,
        IMsqClient msqClient,
        CancellationToken ct)
    {
        switch (message.Action)
        {
            case OutboxActions.SendImagesToContourTool:
            case "SEND_TO_PVMED_AUTOCONTOUR":
                await pvMedClient.SendAutoContourAsync(message.PayloadJson, ct);
                break;
            case OutboxActions.SendToMonacoImport:
            case "SEND_TO_MONACO_IMPORT":
                if (message.CaseId is null)
                    throw new InvalidOperationException("Outbox message CaseId is required for Monaco import.");
                await monacoAdapter.SendToMonacoImportAsync(message.CaseId.Value, message.PayloadJson, ct);
                break;
            case OutboxActions.QueryContourStatus:
                await pvMedClient.QueryContourStatusAsync(message.PayloadJson, ct);
                break;
            case OutboxActions.GeneratePrescription:
                await msqClient.GeneratePrescriptionAsync(message.PayloadJson, ct);
                break;
            case OutboxActions.SyncSchedule:
                await msqClient.SyncScheduleAsync(message.PayloadJson, ct);
                break;
            case OutboxActions.QueryTreatmentProgress:
                await msqClient.QueryTreatmentProgressAsync(message.PayloadJson, ct);
                break;
            default:
                throw new InvalidOperationException($"Unknown outbox action '{message.Action}'.");
        }
    }

    private void HandleFailure(
        OutboxMessageEntity message,
        Exception ex,
        DateTimeOffset now,
        OutboxRetryPolicy policy)
    {
        message.RetryCount += 1;
        message.LastTriedAt = now;

        if (message.RetryCount >= policy.MaxAttempts)
        {
            message.Status = OutboxStatus.Failed;
            message.NextRetryAt = null;
        }
        else
        {
            message.Status = OutboxStatus.Retrying;
            message.NextRetryAt = now.Add(policy.ComputeNextDelay(message.RetryCount));
        }

        _logger.LogError(ex,
            "Outbox message {MessageId} action {Action} attempt {Attempt}/{MaxAttempts} failed; " +
            "status set to {Status}, nextRetryAt {NextRetryAt}",
            message.MessageId, message.Action, message.RetryCount, policy.MaxAttempts,
            message.Status, message.NextRetryAt);
    }

    private async Task EscalateToCompensationAsync(
        OutboxMessageEntity message,
        Exception ex,
        IWorkflowDataAccess dataAccess,
        IWorkflowCompensationService compensation,
        CancellationToken ct)
    {
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

        if (string.IsNullOrEmpty(failedStepCode) || message.CaseId is null) return;

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
