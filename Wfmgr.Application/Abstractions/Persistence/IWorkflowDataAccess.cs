using Wfmgr.Application.Abstractions.Persistence.Models;

namespace Wfmgr.Application.Abstractions.Persistence;

public interface IWorkflowDataAccess
{
    Task<CaseData?> GetCaseByIdAsync(Guid caseId, CancellationToken ct);
    Task<CaseData?> GetCaseByAccessionNumberAsync(string accessionNumber, CancellationToken ct);
    Task<CaseFormData?> GetCaseFormByIdAsync(Guid formId, CancellationToken ct);
    Task<IReadOnlyList<CaseFormData>> GetCaseFormsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<CaseFormData?> GetLatestCaseFormByCaseAndTypeAsync(Guid caseId, string formType, CancellationToken ct);
    Task<IReadOnlyList<CaseData>> GetCasesAsync(CancellationToken ct);
    Task<IReadOnlyList<CaseData>> GetCasesByPatientIdAsync(string patientId, CancellationToken ct);
    Task<IReadOnlyList<WorkItemData>> GetWorkItemsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<AuditLogData>> GetAuditLogsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<AuditLogData>> GetAuditLogsAsync(CancellationToken ct);
    Task<IReadOnlyList<CaseTransitionHistoryData>> GetCaseTransitionHistoryByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<CaseAttachmentData>> GetCaseAttachmentsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<ExternalEventData>> GetExternalEventsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<IntegrationReferenceData>> GetIntegrationReferencesByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<IReadOnlyList<PlanVersionData>> GetPlanVersionsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<bool> ExternalEventExistsAsync(string source, string type, string externalId, CancellationToken ct);
    Task<ExternalEventData?> GetExternalEventAsync(string source, string type, string externalId, CancellationToken ct);
    Task<IReadOnlyList<ExternalEventData>> GetExternalEventsByCaseAsync(Guid caseId, string source, string type, CancellationToken ct);
    Task<WorkItemData?> GetWorkItemByIdAsync(Guid workItemId, CancellationToken ct);
    Task<WorkItemData?> GetOpenWorkItemAsync(Guid caseId, string type, CancellationToken ct);
    Task<IReadOnlyList<WorkItemData>> GetMutableWorkItemsByCaseIdAsync(Guid caseId, CancellationToken ct);
    Task<bool> WorkItemExistsAsync(Guid caseId, string type, string? requiredResultCode, CancellationToken ct);
    Task<bool> CaseFormExistsAsync(Guid caseId, string formType, string? requiredStatus, CancellationToken ct);
    Task<bool> PlanVersionExistsAsync(Guid caseId, CancellationToken ct);
    Task AddCaseAsync(CaseData item, CancellationToken ct);
    Task UpdateCaseAsync(CaseData item, CancellationToken ct);
    Task AddCaseFormAsync(CaseFormData item, CancellationToken ct);
    Task UpdateCaseFormAsync(CaseFormData item, CancellationToken ct);
    Task AddWorkItemAsync(WorkItemData item, CancellationToken ct);
    Task AddExternalEventAsync(ExternalEventData item, CancellationToken ct);
    Task AddOutboxMessageAsync(OutboxMessageData item, CancellationToken ct);
    Task EnqueueOutboxAsync(Guid? caseId, string targetSystem, string action, string payloadJson, CancellationToken ct);

    /// <summary>
    /// Typed enqueue carrying the wire contract type, schema version, correlation id, and
    /// W3C traceparent. Prefer this overload for new call sites; the untyped version is
    /// retained for back-compat.
    /// </summary>
    Task EnqueueOutboxAsync(
        Guid? caseId,
        string targetSystem,
        string action,
        string payloadJson,
        string? messageType,
        int schemaVersion,
        Guid? correlationId,
        string? traceparent,
        Wfmgr.Domain.Integrations.OutboxDeliveryMode deliveryMode,
        CancellationToken ct);

    /// <summary>
    /// Records an inbound external event in the dedup inbox.
    /// Returns <c>false</c> if the <c>(integration, externalEventId)</c> pair already
    /// exists — the caller MUST treat the event as a duplicate and skip processing.
    /// Returns <c>true</c> if the row was newly inserted.
    /// </summary>
    Task<bool> TryReserveExternalEventInboxAsync(
        string integration,
        string externalEventId,
        string? messageType,
        string? payloadHash,
        Guid? caseId,
        string? traceparent,
        CancellationToken ct);

    /// <summary>Marks an inbox row as processed (sets <c>ProcessedAt</c>, fills <c>CaseId</c> if now known).</summary>
    Task MarkExternalEventInboxProcessedAsync(
        string integration,
        string externalEventId,
        Guid? caseId,
        CancellationToken ct);
    Task MarkExternalEventProcessedAsync(
        Guid eventId,
        Guid? caseId,
        CancellationToken ct);
    Task AddAuditLogAsync(AuditLogData item, CancellationToken ct);
    Task AddCaseTransitionHistoryAsync(CaseTransitionHistoryData item, CancellationToken ct);
    Task UpsertIntegrationReferenceAsync(
        Guid caseId,
        string systemName,
        string externalEntityType,
        string externalId,
        string? externalStatus,
        string? metadataJson,
        CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
