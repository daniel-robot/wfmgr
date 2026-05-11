using Wfmgr.Application.Workflows.V1.Dtos;

namespace Wfmgr.Application.Workflows.V1;

public interface ICaseWorkflowService
{
    Task<Guid> CreateCaseAsync(CreateCaseRequest request, CancellationToken ct);
    Task CompleteDailyImageScanAsync(Guid caseId, string? completedBy, CancellationToken ct, IReadOnlyCollection<string>? actorRoles = null);
    Task HandleCtImageStoredAsync(CtImageStoredRequest request, CancellationToken ct);
    Task HandlePvMedEventAsync(PvMedEventRequest request, CancellationToken ct);
    Task ForwardToMonacoAsync(Guid caseId, CancellationToken ct);
    Task CompleteManualContouringAsync(Guid caseId, CancellationToken ct, IReadOnlyCollection<string>? actorRoles = null);
    Task RejectPlanReviewAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct, IReadOnlyCollection<string>? actorRoles = null);
    Task RejectPlanReReviewAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct, IReadOnlyCollection<string>? actorRoles = null);
    Task FailQaAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct, IReadOnlyCollection<string>? actorRoles = null);
    Task CancelCaseAsync(Guid caseId, string reason, string triggeredBy, CancellationToken ct, IReadOnlyCollection<string>? actorRoles = null);
}
