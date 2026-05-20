using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wfmgr.Api.Auth;
using Wfmgr.Application.Workflows.V1.CaseStatuses;
using Wfmgr.Application.Workflows.V1.Config;
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Application.Workflows.V1.Gates;
using Wfmgr.Application.Workflows.V1.Vocabulary;
using Wfmgr.Domain.Forms;

namespace Wfmgr.Api.Controllers;

/// <summary>
/// Read-only meta endpoints exposing the in-code constants used as enum-like
/// vocabularies in workflow transition rows: case statuses, trigger types,
/// gate-check names, work-item types, roles, slot codes, and the distinct
/// success/failure action strings observed in the current catalog.
/// </summary>
[ApiController]
[Route("api/workflow-meta")]
[Authorize(Policy = WorkflowConfigPolicies.Admin)]
public class WorkflowMetaController : ControllerBase
{
    private readonly IWorkflowTransitionCatalogService _service;
    private readonly IWorkflowVocabularyCatalogService _vocabulary;
    private readonly ICaseStatusOverlayService _caseStatusOverlay;

    public WorkflowMetaController(
        IWorkflowTransitionCatalogService service,
        IWorkflowVocabularyCatalogService vocabulary,
        ICaseStatusOverlayService caseStatusOverlay)
    {
        _service = service;
        _vocabulary = vocabulary;
        _caseStatusOverlay = caseStatusOverlay;
    }

    [HttpGet]
    [ProducesResponseType(typeof(WorkflowMetaCatalogDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkflowMetaCatalogDto>> Get(CancellationToken ct)
    {
        var transitions = await _service.ListAllAsync(ct);

        var sideEffectActions = transitions
            .SelectMany(t => t.SuccessActions.Concat(t.FailureActions))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .Select(s => new WorkflowMetaItemDto(s, null))
            .ToList();

        var allTerms = await _vocabulary.ListAllAsync(ct);
        var overlays = await _caseStatusOverlay.ListAllAsync(ct);
        var caseStatusItems = overlays
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.Code, StringComparer.Ordinal)
            .Select(o => new WorkflowMetaItemDto(o.Code, o.DisplayName ?? o.Description))
            .ToList();

        var dto = new WorkflowMetaCatalogDto(
            CaseStatuses: caseStatusItems,
            WorkItemTypes: MergeWithVocabulary(WorkflowTransitionGraphValidator.Catalogs.WorkItemTypes, allTerms, WorkflowVocabularyKinds.WorkItemType),
            CaseFormTypes: MergeWithVocabulary(StringConstants(typeof(CaseFormTypes)), allTerms, WorkflowVocabularyKinds.CaseFormType),
            Roles: MergeWithVocabulary(WorkflowTransitionGraphValidator.Catalogs.Roles, allTerms, WorkflowVocabularyKinds.Role),
            GateChecks: ToSortedItems(WorkflowTransitionGraphValidator.Catalogs.GateChecks),
            SideEffectActions: sideEffectActions,
            TriggerTypes: ToSortedItems(WorkflowTransitionGraphValidator.Catalogs.TriggerTypes),
            SlotCodes: ToSortedItems(WorkflowTransitionGraphValidator.Catalogs.SlotCodes));

        return Ok(dto);
    }

    private static List<WorkflowMetaItemDto> ToSortedItems(IEnumerable<string> values) => values
        .OrderBy(s => s, StringComparer.Ordinal)
        .Select(s => new WorkflowMetaItemDto(s, null))
        .ToList();

    /// <summary>
    /// Merges static constants with DB-backed vocabulary terms for the given kind.
    /// DB terms supply descriptions (display name preferred over description); only
    /// enabled DB terms not already in the static list are added; disabled terms
    /// shadowing a static code are still included so admins can see they're disabled.
    /// </summary>
    private static List<WorkflowMetaItemDto> MergeWithVocabulary(
        IEnumerable<string> staticCodes,
        IReadOnlyList<WorkflowVocabularyTermDto> allTerms,
        string kind)
    {
        var termsByCode = allTerms
            .Where(t => t.Kind == kind)
            .ToDictionary(t => t.Code, StringComparer.Ordinal);

        var combined = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var c in staticCodes) combined[c] = null;
        foreach (var t in termsByCode.Values)
        {
            if (!t.IsEnabled && !combined.ContainsKey(t.Code)) continue;
            combined[t.Code] = t.DisplayName ?? t.Description;
        }

        return combined
            .Select(kvp => new WorkflowMetaItemDto(kvp.Key, kvp.Value))
            .ToList();
    }

    private static IEnumerable<string> StringConstants(Type t) => t
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!);
}
