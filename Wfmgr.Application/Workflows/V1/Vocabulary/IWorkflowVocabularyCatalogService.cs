namespace Wfmgr.Application.Workflows.V1.Vocabulary;

/// <summary>
/// DB-backed catalog of workflow-vocabulary terms (roles, work-item types, form types).
/// Lazily seeded from in-code constants on first read; mutable through the admin API.
/// </summary>
public interface IWorkflowVocabularyCatalogService
{
    /// <summary>Returns all terms across all kinds.</summary>
    Task<IReadOnlyList<WorkflowVocabularyTermDto>> ListAllAsync(CancellationToken ct);

    /// <summary>Returns all terms for a single kind.</summary>
    Task<IReadOnlyList<WorkflowVocabularyTermDto>> ListByKindAsync(string kind, CancellationToken ct);

    Task<WorkflowVocabularyTermDto?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<WorkflowVocabularyTermDto?> GetByCodeAsync(string kind, string code, CancellationToken ct);

    /// <summary>Returns the codes of all enabled terms for a kind (used by transition validator).</summary>
    Task<IReadOnlyCollection<string>> GetEnabledCodesAsync(string kind, CancellationToken ct);

    Task<ValidateWorkflowVocabularyTermResponse> ValidateAsync(
        string kind, string code, CancellationToken ct);

    Task<WorkflowVocabularyMutationResult> CreateAsync(
        CreateWorkflowVocabularyTermRequest request, string? actorId, CancellationToken ct);

    Task<WorkflowVocabularyMutationResult> UpdateAsync(
        Guid id, UpdateWorkflowVocabularyTermRequest request, string? actorId, CancellationToken ct);

    Task<WorkflowVocabularyMutationResult> SetEnabledAsync(
        Guid id, bool enabled, ToggleWorkflowVocabularyTermRequest request, string? actorId, CancellationToken ct);

    Task<WorkflowVocabularyMutationResult> DeleteAsync(
        Guid id, ToggleWorkflowVocabularyTermRequest request, string? actorId, CancellationToken ct);

    Task<IReadOnlyList<WorkflowVocabularyChangeLogDto>> GetChangeLogAsync(
        Guid termId, int limit, CancellationToken ct);

    void InvalidateCache();
}
