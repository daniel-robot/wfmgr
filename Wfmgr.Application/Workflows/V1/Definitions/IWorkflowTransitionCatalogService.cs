using Wfmgr.Application.Workflows.V1.Definitions;

namespace Wfmgr.Application.Workflows.V1.Definitions;

/// <summary>
/// DB-backed source of truth for workflow transition definitions.
/// <para>
/// The static <see cref="WorkflowTransitionCatalog"/> is retained as the seed source
/// for first-time-empty databases and as the in-memory contract used by tests and
/// developer documentation. At runtime, services should depend on this interface
/// instead of <c>WorkflowTransitionCatalog.All</c> / <c>ByCode</c>.
/// </para>
/// </summary>
public interface IWorkflowTransitionCatalogService
{
    /// <summary>All transitions in canonical (Phase, SortOrder) order.</summary>
    Task<IReadOnlyList<TransitionDefinition>> GetAllAsync(CancellationToken ct);

    /// <summary>Single transition by business code (e.g. "SIM-001"); null if not found.</summary>
    Task<TransitionDefinition?> FindByCodeAsync(string code, CancellationToken ct);

    /// <summary>
    /// First transition matching a (trigger, fromStatus) pair — mirrors the previous
    /// <c>WorkflowTransitionCatalog.All.FirstOrDefault(...)</c> lookup pattern.
    /// </summary>
    Task<TransitionDefinition?> FindByTriggerAsync(string triggerName, string fromStatus, CancellationToken ct);

    /// <summary>Invalidates the in-memory cache. Called after any mutation.</summary>
    void InvalidateCache();

    // ── Admin / mutation surface (Phase 2) ────────────────────────────────────

    /// <summary>List all transitions (including disabled) as DTOs for the admin UI.</summary>
    Task<IReadOnlyList<WorkflowTransitionDto>> ListAllAsync(CancellationToken ct);

    Task<WorkflowTransitionDto?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<WorkflowTransitionDto?> GetByCodeAsync(string code, CancellationToken ct);

    Task<ValidateWorkflowTransitionResponse> ValidateAsync(
        string code,
        string toStatus,
        string triggerType,
        IReadOnlyList<string> fromStatuses,
        IReadOnlyList<string>? requiredRoles,
        IReadOnlyList<string>? gateChecks,
        IReadOnlyList<string>? successActions,
        IReadOnlyList<string>? failureActions,
        IReadOnlyList<string>? workItemsToCreate,
        string? configSlot,
        CancellationToken ct);

    Task<WorkflowTransitionMutationResult> CreateAsync(
        CreateWorkflowTransitionRequest request,
        string? actorId,
        CancellationToken ct);

    Task<WorkflowTransitionMutationResult> UpdateAsync(
        Guid id,
        UpdateWorkflowTransitionRequest request,
        string? actorId,
        CancellationToken ct);

    Task<WorkflowTransitionMutationResult> SetEnabledAsync(
        Guid id,
        bool enabled,
        ToggleWorkflowTransitionRequest request,
        string? actorId,
        CancellationToken ct);

    Task<WorkflowTransitionMutationResult> DeleteAsync(
        Guid id,
        ToggleWorkflowTransitionRequest request,
        string? actorId,
        CancellationToken ct);

    Task<IReadOnlyList<WorkflowTransitionChangeLogDto>> GetChangeLogAsync(
        Guid transitionId,
        int limit,
        CancellationToken ct);
}
