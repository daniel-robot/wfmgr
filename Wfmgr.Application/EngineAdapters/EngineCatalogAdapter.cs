using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Domain.Enums;
using EngineAbstractions = Wfmgr.Engine.Abstractions;
using EngineCore = Wfmgr.Engine.Core;

namespace Wfmgr.Application.EngineAdapters;

/// <summary>
/// Implements the engine-level <see cref="EngineAbstractions.ITransitionCatalogService"/> by delegating
/// to the host's <see cref="IWorkflowTransitionCatalogService"/>.
/// Maps between engine-level string-based definitions and host-level domain-type-based definitions.
/// </summary>
internal sealed class EngineCatalogAdapter : EngineAbstractions.ITransitionCatalogService
{
    private readonly IWorkflowTransitionCatalogService _inner;

    public EngineCatalogAdapter(IWorkflowTransitionCatalogService inner)
    {
        _inner = inner;
    }

    public async Task<IReadOnlyList<EngineCore.TransitionDefinition>> GetAllAsync(CancellationToken ct)
    {
        var all = await _inner.GetAllAsync(ct);
        return all.Select(MapToEngine).ToList();
    }

    public async Task<EngineCore.TransitionDefinition?> FindByCodeAsync(string code, CancellationToken ct)
    {
        var found = await _inner.FindByCodeAsync(code, ct);
        return found is not null ? MapToEngine(found) : null;
    }

    public async Task<EngineCore.TransitionDefinition?> FindByTriggerAsync(string triggerName, string fromStatus, CancellationToken ct)
    {
        var found = await _inner.FindByTriggerAsync(triggerName, fromStatus, ct);
        return found is not null ? MapToEngine(found) : null;
    }

    public void InvalidateCache() => _inner.InvalidateCache();

    private static EngineCore.TransitionDefinition MapToEngine(TransitionDefinition d) =>
        new()
        {
            Code = d.Code,
            TriggerName = d.TriggerName,
            TriggerType = d.TriggerType.ToString(),
            FromStatuses = d.FromStatuses.Select(s => s.ToString()).ToArray(),
            ToStatus = d.ToStatus.ToString(),
            RequiredRoles = d.RequiredRoles,
            GateChecks = d.GateChecks,
            SuccessActions = d.SuccessActions,
            FailureActions = d.FailureActions,
            WorkItemsToCreate = d.WorkItemsToCreate,
            ConfigSlot = d.ConfigSlot,
        };
}
