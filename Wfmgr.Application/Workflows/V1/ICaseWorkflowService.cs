using Wfmgr.Application.Workflows.V1.Dtos;

namespace Wfmgr.Application.Workflows.V1;

public interface ICaseWorkflowService
{
    Task<Guid> CreateCaseAsync(CreateCaseRequest request, CancellationToken ct);
    Task CompleteDailyImageScanAsync(Guid caseId, string? completedBy, CancellationToken ct);
    Task HandleCtImageStoredAsync(CtImageStoredRequest request, CancellationToken ct);
    Task HandlePvMedEventAsync(PvMedEventRequest request, CancellationToken ct);
    Task ForwardToMonacoAsync(Guid caseId, CancellationToken ct);
    Task CompleteManualContouringAsync(Guid caseId, CancellationToken ct);
    Task RejectPlanReviewAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct);
    Task RejectPlanReReviewAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct);
    Task FailQaAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct);
    Task CancelCaseAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct);
}
