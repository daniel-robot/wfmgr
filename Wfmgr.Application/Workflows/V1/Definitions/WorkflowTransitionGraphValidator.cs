using System.Reflection;
using Wfmgr.Application.Workflows.V1.Gates;
using Wfmgr.Domain;
using Wfmgr.Domain.Enums;
using Wfmgr.Domain.Forms;
using Wfmgr.Domain.WorkItems;

namespace Wfmgr.Application.Workflows.V1.Definitions;

/// <summary>
/// Stateless validator for proposed workflow-transition mutations. Checks individual
/// row well-formedness (unknown enum values, unknown gate names, etc.) plus optional
/// graph-level invariants when invoked with <see cref="ValidateGraph"/>.
/// </summary>
public static class WorkflowTransitionGraphValidator
{
    private static readonly HashSet<string> KnownCaseStatuses =
        Enum.GetNames<CaseStatus>().ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> KnownTriggerTypes =
        Enum.GetNames<WorkflowTriggerType>().ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> KnownGateChecks =
        ConstantsOf(typeof(GateCheckNames)).ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> KnownWorkItemTypes =
        ConstantsOf(typeof(WorkItemTypes))
            .Concat(ConstantsOf(typeof(CaseFormTypes)))
            .ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> KnownRoles =
        ConstantsOf(typeof(WorkflowRoles)).ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> KnownSlotCodes =
        ConstantsOf(typeof(WorkflowSlotCodes)).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Validates a single transition's fields. Returns a list of errors (blocking)
    /// and warnings (non-blocking). Empty errors = OK to save.
    /// </summary>
    public static (IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings) ValidateOne(
        string code,
        string toStatus,
        string triggerType,
        IReadOnlyList<string> fromStatuses,
        IReadOnlyList<string> requiredRoles,
        IReadOnlyList<string> gateChecks,
        IReadOnlyList<string> successActions,
        IReadOnlyList<string> failureActions,
        IReadOnlyList<string> workItemsToCreate,
        string? configSlot,
        IReadOnlyCollection<string>? extraKnownRoles = null,
        IReadOnlyCollection<string>? extraKnownWorkItemTypes = null)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var roleSet = extraKnownRoles is { Count: > 0 }
            ? new HashSet<string>(KnownRoles.Concat(extraKnownRoles), StringComparer.Ordinal)
            : KnownRoles;
        var workItemSet = extraKnownWorkItemTypes is { Count: > 0 }
            ? new HashSet<string>(KnownWorkItemTypes.Concat(extraKnownWorkItemTypes), StringComparer.Ordinal)
            : KnownWorkItemTypes;

        if (string.IsNullOrWhiteSpace(code))
        {
            errors.Add("code is required.");
        }

        if (!KnownCaseStatuses.Contains(toStatus))
        {
            errors.Add($"toStatus '{toStatus}' is not a known CaseStatus value.");
        }

        if (!KnownTriggerTypes.Contains(triggerType))
        {
            errors.Add($"triggerType '{triggerType}' is not a known WorkflowTriggerType value.");
        }

        if (fromStatuses is null || fromStatuses.Count == 0)
        {
            errors.Add("fromStatuses must contain at least one CaseStatus value.");
        }
        else
        {
            foreach (var fs in fromStatuses)
            {
                if (!KnownCaseStatuses.Contains(fs))
                {
                    errors.Add($"fromStatus '{fs}' is not a known CaseStatus value.");
                }
            }
            if (fromStatuses.Distinct(StringComparer.Ordinal).Count() != fromStatuses.Count)
            {
                errors.Add("fromStatuses contains duplicates.");
            }
        }

        foreach (var role in requiredRoles ?? [])
        {
            if (!roleSet.Contains(role))
            {
                warnings.Add($"requiredRole '{role}' is not a known WorkflowRoles constant or vocabulary term — it must match a string a caller will present.");
            }
        }

        foreach (var gate in gateChecks ?? [])
        {
            if (!KnownGateChecks.Contains(gate))
            {
                errors.Add($"gateCheck '{gate}' is not registered in GateCheckNames; the gate validator will reject any transition using it.");
            }
        }

        foreach (var wi in workItemsToCreate ?? [])
        {
            if (!workItemSet.Contains(wi))
            {
                warnings.Add($"workItemToCreate '{wi}' is not a known WorkItemTypes / CaseFormTypes constant or vocabulary term.");
            }
        }

        if (!string.IsNullOrWhiteSpace(configSlot) && !KnownSlotCodes.Contains(configSlot!))
        {
            warnings.Add($"configSlot '{configSlot}' is not a known WorkflowSlotCodes constant.");
        }

        // SuccessActions / FailureActions are free-form strings dispatched by name in
        // WorkflowSideEffectService — there's no central registry. Emit a soft warning
        // when they look empty.
        if ((successActions?.Count ?? 0) == 0 && (failureActions?.Count ?? 0) == 0)
        {
            warnings.Add("transition has no successActions or failureActions — no audit/side-effect descriptors will run.");
        }

        return (errors, warnings);
    }

    /// <summary>
    /// Graph-level invariants over a proposed full catalog (all enabled transitions).
    /// Currently checks: every <c>ToStatus</c> except Cancelled is reachable from
    /// <see cref="CaseStatus.Submitted"/>; no transition has duplicate (TriggerName, FromStatus)
    /// pair shared with another row.
    /// </summary>
    public static (IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings) ValidateGraph(
        IReadOnlyList<(string Code, string TriggerName, IReadOnlyList<string> FromStatuses, string ToStatus)> rows)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Duplicate (trigger, from) detection.
        var byKey = new Dictionary<(string T, string F), string>(); // value = first code
        foreach (var r in rows)
        {
            foreach (var f in r.FromStatuses)
            {
                var key = (r.TriggerName.ToLowerInvariant(), f);
                if (byKey.TryGetValue(key, out var firstCode))
                {
                    errors.Add($"transitions '{firstCode}' and '{r.Code}' both match trigger '{r.TriggerName}' from status '{f}'.");
                }
                else
                {
                    byKey[key] = r.Code;
                }
            }
        }

        // Reachability from Submitted (BFS over (from -> to) edges).
        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            foreach (var f in r.FromStatuses)
            {
                if (!adjacency.TryGetValue(f, out var set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    adjacency[f] = set;
                }
                set.Add(r.ToStatus);
            }
        }

        var reachable = new HashSet<string>(StringComparer.Ordinal) { nameof(CaseStatus.Submitted) };
        var queue = new Queue<string>();
        queue.Enqueue(nameof(CaseStatus.Submitted));
        while (queue.Count > 0)
        {
            var s = queue.Dequeue();
            if (!adjacency.TryGetValue(s, out var nexts)) continue;
            foreach (var n in nexts)
            {
                if (reachable.Add(n)) queue.Enqueue(n);
            }
        }

        var referencedStatuses = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            referencedStatuses.Add(r.ToStatus);
            foreach (var f in r.FromStatuses) referencedStatuses.Add(f);
        }

        foreach (var s in referencedStatuses)
        {
            if (!reachable.Contains(s) && s != nameof(CaseStatus.Submitted))
            {
                warnings.Add($"status '{s}' is referenced by a transition but is unreachable from Submitted.");
            }
        }

        return (errors, warnings);
    }

    private static IEnumerable<string> ConstantsOf(Type t) =>
        t.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Where(v => !string.IsNullOrEmpty(v));

    /// <summary>Exposes the reflected constant sets for the meta endpoint.</summary>
    public static class Catalogs
    {
        public static IReadOnlyCollection<string> CaseStatuses => KnownCaseStatuses;
        public static IReadOnlyCollection<string> TriggerTypes => KnownTriggerTypes;
        public static IReadOnlyCollection<string> GateChecks => KnownGateChecks;
        public static IReadOnlyCollection<string> WorkItemTypes => KnownWorkItemTypes;
        public static IReadOnlyCollection<string> Roles => KnownRoles;
        public static IReadOnlyCollection<string> SlotCodes => KnownSlotCodes;
    }
}
