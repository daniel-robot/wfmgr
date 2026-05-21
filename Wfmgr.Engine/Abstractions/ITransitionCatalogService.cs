using Wfmgr.Engine.Core;

namespace Wfmgr.Engine.Abstractions;

/// <summary>
/// Source of truth for workflow transition definitions.
/// Host implementations map domain status enums to/from strings at the boundary.
/// </summary>
public interface ITransitionCatalogService
{
    Task<IReadOnlyList<TransitionDefinition>> GetAllAsync(CancellationToken ct);
    Task<TransitionDefinition?> FindByCodeAsync(string code, CancellationToken ct);
    Task<TransitionDefinition?> FindByTriggerAsync(string triggerName, string fromStatus, CancellationToken ct);
    void InvalidateCache();
}
