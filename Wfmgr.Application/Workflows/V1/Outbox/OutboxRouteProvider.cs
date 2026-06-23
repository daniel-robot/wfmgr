using System.Collections.ObjectModel;
using Wfmgr.Domain.Integrations;

namespace Wfmgr.Application.Workflows.V1.Outbox;

/// <summary>
/// Full descriptor of where an outbox message should be routed.
/// </summary>
public sealed record OutboxRoute(
    string TargetSystem,
    string Action,
    string MessageType);

/// <summary>
/// Resolves outbox routing from either a <c>FailedStepCode</c> (transition code) or
/// a <c>SuccessAction</c> string.  Provides a single source of truth for the
/// transition-code/system-action mapping that was previously duplicated between
/// <see cref="SideEffects.WorkflowSideEffectService"/> (keyed by SuccessAction) and
/// <see cref="Compensation.WorkflowCompensationService"/> (keyed by FailedStepCode).
/// <para>
/// This is NOT the same concern as <see cref="IOutboxRoutingPolicy"/>, which decides
/// <em>transport</em> (Bus vs. Http).  This provider decides <em>destination</em>
/// and <em>message type</em> — what goes where, not how it gets there.
/// </para>
/// </summary>
public interface IOutboxRouteProvider
{
    /// <summary>
    /// Look up an outbox route by transition <c>FailedStepCode</c> (e.g. <c>"IMG-002"</c>).
    /// Returns <c>null</c> when the code has no compensation retry route.
    /// </summary>
    OutboxRoute? GetRouteByStepCode(string failedStepCode);

    /// <summary>
    /// Look up an outbox route by <c>SuccessAction</c> string (e.g. <c>"CreateOutboxSendImagesToContourTool"</c>).
    /// Returns <c>null</c> when the action has no outbox route.
    /// </summary>
    OutboxRoute? TryGetRouteBySuccessAction(string successAction);
}

public sealed class OutboxRouteProvider : IOutboxRouteProvider
{
    // ── Step-code routes (used by compensation service) ──────────────────────
    // Maps FailedStepCode (transition code like "IMG-002") to the target system
    // and outbox action that a compensation retry message should use.
    // These cover the hardcoded switch in WorkflowCompensationService.BuildRetryOutboxMessage.

    private static readonly IReadOnlyDictionary<string, (string TargetSystem, string Action)> StepCodeRoutes =
        new ReadOnlyDictionary<string, (string, string)>(
            new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
            {
                // Image acquisition / contouring tool resend
                ["IMG-002"] = ("PvMed",   OutboxActions.SendImagesToContourTool),
                ["IMG-003"] = ("PvMed",   OutboxActions.SendImagesToContourTool),

                // Auto-contouring failures — retry sends images again
                ["CON-002"] = ("PvMed",   OutboxActions.SendImagesToContourTool),
                ["CON-003"] = ("PvMed",   OutboxActions.SendImagesToContourTool),
                ["CON-004"] = ("PvMed",   OutboxActions.SendImagesToContourTool),

                // Prescription generation retry
                ["RX-002"]  = ("PvMed",   OutboxActions.GeneratePrescription),
                ["RX-003"]  = ("PvMed",   OutboxActions.GeneratePrescription),
                ["RX-006"]  = ("PvMed",   OutboxActions.GeneratePrescription),
                ["RX-007"]  = ("PvMed",   OutboxActions.GeneratePrescription),

                // Schedule sync retry
                ["TRT-001"] = ("MSQ",     OutboxActions.SyncSchedule),
                ["TRT-002"] = ("MSQ",     OutboxActions.SyncSchedule),

                // Treatment progress polling retry
                ["TRT-012"] = ("Monaco",  OutboxActions.QueryTreatmentProgress),
            });

    // ── Success-action routes (used by side-effect service) ──────────────────
    // Maps SuccessAction identifiers to full outbox descriptors including
    // the wire-format message type.  These were the OutboxActionMap in
    // WorkflowSideEffectService.

    private static readonly IReadOnlyDictionary<string, OutboxRoute> SuccessActionRoutes =
        new ReadOnlyDictionary<string, OutboxRoute>(
            new Dictionary<string, OutboxRoute>(StringComparer.OrdinalIgnoreCase)
            {
                // Contouring tool — target system resolved at runtime from S1 profile.
                ["CreateOutboxSendImagesToContourTool"] = new("PvMed",  OutboxActions.SendImagesToContourTool, typeof(Wfmgr.Contracts.Contouring.SendImagesToContourTool.V1).FullName!),
                ["CreateOutboxRestartContouring"]       = new("PvMed",  OutboxActions.SendImagesToContourTool, typeof(Wfmgr.Contracts.Contouring.SendImagesToContourTool.V1).FullName!),

                // Monaco import.
                ["SendToMonacoImport"]                  = new("Monaco", OutboxActions.SendToMonacoImport,      typeof(Wfmgr.Contracts.Monaco.SendToMonacoImport.V1).FullName!),

                // Prescription generation / retry.
                ["CreateOutboxGeneratePrescription"]    = new("PvMed",  OutboxActions.GeneratePrescription,    typeof(Wfmgr.Contracts.Prescription.GeneratePrescription.V1).FullName!),
                ["CreateOutboxPrescriptionSync"]        = new("PvMed",  OutboxActions.GeneratePrescription,    typeof(Wfmgr.Contracts.Prescription.GeneratePrescription.V1).FullName!),

                // Schedule synchronisation.
                ["StartScheduleWatch"]                  = new("MSQ",    OutboxActions.SyncSchedule,            typeof(Wfmgr.Contracts.Scheduling.SyncSchedule.V1).FullName!),

                // Treatment progress polling.
                ["CreateTreatmentMonitor"]              = new("Monaco", OutboxActions.QueryTreatmentProgress,  typeof(Wfmgr.Contracts.Monaco.QueryTreatmentProgress.V1).FullName!),
                ["UpdateProgress"]                      = new("Monaco", OutboxActions.QueryTreatmentProgress,  typeof(Wfmgr.Contracts.Monaco.QueryTreatmentProgress.V1).FullName!),
            });

    public OutboxRoute? GetRouteByStepCode(string failedStepCode)
    {
        if (StepCodeRoutes.TryGetValue(failedStepCode, out var tuple))
        {
            // Find the MessageType from the success-action side so the compensation
            // retry also carries the contract type (matching how the side-effect
            // service enqueues).  Look up by the action name; reverse-map back.
            var messageType = SuccessActionRoutes.Values
                .FirstOrDefault(r =>
                    string.Equals(r.Action, tuple.Action, StringComparison.OrdinalIgnoreCase))
                ?.MessageType;

            return new OutboxRoute(tuple.TargetSystem, tuple.Action, messageType ?? string.Empty);
        }

        return null;
    }

    public OutboxRoute? TryGetRouteBySuccessAction(string successAction) =>
        SuccessActionRoutes.TryGetValue(successAction, out var route)
            ? route
            : null;

    /// <summary>
    /// All step codes that have registered routes — useful for coverage assertions.
    /// </summary>
    public static IReadOnlyCollection<string> AllStepCodes => (IReadOnlyCollection<string>)StepCodeRoutes.Keys;

    /// <summary>
    /// All success actions that have registered routes — useful for coverage assertions.
    /// </summary>
    public static IReadOnlyCollection<string> AllSuccessActions => (IReadOnlyCollection<string>)SuccessActionRoutes.Keys;
}
