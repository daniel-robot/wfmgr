using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Application.Workflows.V1.Outbox;
using Wfmgr.Domain.Integrations;
using Xunit;

namespace Wfmgr.Api.Tests;

/// <summary>
/// Tests for <see cref="OutboxRouteProvider"/> — the central source of truth for
/// transition-code/action-to-outbox routing used by both
/// <see cref="SideEffects.WorkflowSideEffectService"/> and
/// <see cref="Compensation.WorkflowCompensationService"/>.
/// </summary>
public class OutboxRouteProviderTests
{
    private static readonly OutboxRouteProvider Provider = new();

    // ── Step-code (compensation) route coverage ─────────────────────────────

    /// <summary>
    /// Every hardcoded route from the old switch has been preserved exactly.
    /// </summary>
    [Fact]
    public void GetRouteByStepCode_ReturnsExpectedMapping_ForAllKnownCodes()
    {
        var r1 = Provider.GetRouteByStepCode("IMG-002");
        Assert.NotNull(r1);
        Assert.Equal("PvMed", r1!.TargetSystem);
        Assert.Equal(OutboxActions.SendImagesToContourTool, r1.Action);

        var r2 = Provider.GetRouteByStepCode("IMG-003");
        Assert.NotNull(r2);
        Assert.Equal("PvMed", r2!.TargetSystem);
        Assert.Equal(OutboxActions.SendImagesToContourTool, r2.Action);

        var r3 = Provider.GetRouteByStepCode("CON-002");
        Assert.NotNull(r3);
        Assert.Equal("PvMed", r3!.TargetSystem);
        Assert.Equal(OutboxActions.SendImagesToContourTool, r3.Action);

        var r4 = Provider.GetRouteByStepCode("CON-003");
        Assert.NotNull(r4);
        Assert.Equal("PvMed", r4!.TargetSystem);
        Assert.Equal(OutboxActions.SendImagesToContourTool, r4.Action);

        var r5 = Provider.GetRouteByStepCode("CON-004");
        Assert.NotNull(r5);
        Assert.Equal("PvMed", r5!.TargetSystem);
        Assert.Equal(OutboxActions.SendImagesToContourTool, r5.Action);

        var r6 = Provider.GetRouteByStepCode("RX-002");
        Assert.NotNull(r6);
        Assert.Equal("PvMed", r6!.TargetSystem);
        Assert.Equal(OutboxActions.GeneratePrescription, r6.Action);

        var r7 = Provider.GetRouteByStepCode("RX-003");
        Assert.NotNull(r7);
        Assert.Equal("PvMed", r7!.TargetSystem);
        Assert.Equal(OutboxActions.GeneratePrescription, r7.Action);

        var r8 = Provider.GetRouteByStepCode("RX-006");
        Assert.NotNull(r8);
        Assert.Equal("PvMed", r8!.TargetSystem);
        Assert.Equal(OutboxActions.GeneratePrescription, r8.Action);

        var r9 = Provider.GetRouteByStepCode("RX-007");
        Assert.NotNull(r9);
        Assert.Equal("PvMed", r9!.TargetSystem);
        Assert.Equal(OutboxActions.GeneratePrescription, r9.Action);

        var r10 = Provider.GetRouteByStepCode("TRT-001");
        Assert.NotNull(r10);
        Assert.Equal("MSQ", r10!.TargetSystem);
        Assert.Equal(OutboxActions.SyncSchedule, r10.Action);

        var r11 = Provider.GetRouteByStepCode("TRT-002");
        Assert.NotNull(r11);
        Assert.Equal("MSQ", r11!.TargetSystem);
        Assert.Equal(OutboxActions.SyncSchedule, r11.Action);

        var r12 = Provider.GetRouteByStepCode("TRT-012");
        Assert.NotNull(r12);
        Assert.Equal("Monaco", r12!.TargetSystem);
        Assert.Equal(OutboxActions.QueryTreatmentProgress, r12.Action);
    }

    /// <summary>
    /// Unknown step codes return null (no match), preserving the old fallback
    /// behaviour of falling through to context.SourceSystem / QueryContourStatus
    /// in the compensation service.
    /// </summary>
    [Fact]
    public void GetRouteByStepCode_ReturnsNull_ForUnknownCode()
    {
        Assert.Null(Provider.GetRouteByStepCode("UNKNOWN-999"));
        Assert.Null(Provider.GetRouteByStepCode(""));
        Assert.Null(Provider.GetRouteByStepCode("PLN-005")); // has compensation but no retry route
        Assert.Null(Provider.GetRouteByStepCode("RX-004"));  // has compensation but no retry route
    }

    // ── Success-action (side-effect) route coverage ────────────────────────

    /// <summary>
    /// Every route from the old OutboxActionMap is preserved exactly, including
    /// the full message type (contract type name).
    /// </summary>
    [Fact]
    public void TryGetRouteBySuccessAction_ReturnsExpectedRoute_ForAllKnownActions()
    {
        var r1 = Provider.TryGetRouteBySuccessAction("CreateOutboxSendImagesToContourTool");
        Assert.NotNull(r1);
        Assert.Equal("PvMed", r1!.TargetSystem);
        Assert.Equal(OutboxActions.SendImagesToContourTool, r1.Action);
        Assert.Equal(typeof(Wfmgr.Contracts.Contouring.SendImagesToContourTool.V1).FullName, r1.MessageType);

        var r2 = Provider.TryGetRouteBySuccessAction("CreateOutboxRestartContouring");
        Assert.NotNull(r2);
        Assert.Equal("PvMed", r2!.TargetSystem);
        Assert.Equal(OutboxActions.SendImagesToContourTool, r2.Action);

        var r3 = Provider.TryGetRouteBySuccessAction("SendToMonacoImport");
        Assert.NotNull(r3);
        Assert.Equal("Monaco", r3!.TargetSystem);
        Assert.Equal(OutboxActions.SendToMonacoImport, r3.Action);

        var r4 = Provider.TryGetRouteBySuccessAction("CreateOutboxGeneratePrescription");
        Assert.NotNull(r4);
        Assert.Equal("PvMed", r4!.TargetSystem);
        Assert.Equal(OutboxActions.GeneratePrescription, r4.Action);

        var r5 = Provider.TryGetRouteBySuccessAction("CreateOutboxPrescriptionSync");
        Assert.NotNull(r5);
        Assert.Equal("PvMed", r5!.TargetSystem);
        Assert.Equal(OutboxActions.GeneratePrescription, r5.Action);

        var r6 = Provider.TryGetRouteBySuccessAction("StartScheduleWatch");
        Assert.NotNull(r6);
        Assert.Equal("MSQ", r6!.TargetSystem);
        Assert.Equal(OutboxActions.SyncSchedule, r6.Action);

        var r7 = Provider.TryGetRouteBySuccessAction("CreateTreatmentMonitor");
        Assert.NotNull(r7);
        Assert.Equal("Monaco", r7!.TargetSystem);
        Assert.Equal(OutboxActions.QueryTreatmentProgress, r7.Action);

        var r8 = Provider.TryGetRouteBySuccessAction("UpdateProgress");
        Assert.NotNull(r8);
        Assert.Equal("Monaco", r8!.TargetSystem);
        Assert.Equal(OutboxActions.QueryTreatmentProgress, r8.Action);
    }

    [Fact]
    public void TryGetRouteBySuccessAction_ReturnsNull_ForUnknownAction()
    {
        Assert.Null(Provider.TryGetRouteBySuccessAction("NonExistentAction"));
        Assert.Null(Provider.TryGetRouteBySuccessAction(""));
    }

    // ── Drift detection: catalog cross-reference ───────────────────────────

    /// <summary>
    /// Every transition code that appears as a FailedStepCode in the compensation
    /// catalog and requires a retry outbox message should have a route in the
    /// provider, OR be explicitly documented as a non-retry compensation.
    /// This test will fail when new compensations with retry policies are added
    /// but no route is registered — catching the drift that the old dual-map
    /// design silently allowed.
    ///
    /// Codes that are deliberately excluded from the route table (e.g. REV-003,
    /// PLN-005, QA-003, SIM-005) are those whose compensations have RetryPolicy = null
    /// or ManualInterventionRequired = true, meaning they never auto-enqueue a retry.
    /// </summary>
    [Fact]
    public void AllCompensationFailedStepCodes_WithRetryPolicy_HaveRoute()
    {
        var missing = new List<string>();

        foreach (var def in WorkflowCompensationCatalog.All)
        {
            // Skip codes that don't trigger retry outbox messages.
            if (def.RetryPolicy is null)
                continue;
            if (def.FailedStepCode == "ANY_EXTERNAL_EVENT")
                continue;

            var route = Provider.GetRouteByStepCode(def.FailedStepCode);
            if (route is null)
                missing.Add(def.FailedStepCode);
        }

        Assert.Empty(missing);
    }

    /// <summary>
    /// Every SuccessAction in the transition catalog that references an outbox
    /// action has a corresponding route. This detects drift when new transitions
    /// with outbox-producing side effects are added but no route is registered.
    /// </summary>
    [Fact]
    public void AllTransitionSuccessActions_ReferencingOutbox_HaveRoute()
    {
        var missing = new List<string>();

        foreach (var transition in WorkflowTransitionCatalog.All)
        {
            foreach (var action in transition.SuccessActions)
            {
                // Only actions that look outbox-related need routes.
                // The provider silently returns null for non-outbox actions;
                // any action starting with "CreateOutbox" or containing known
                // integration keywords should have a route.
                if (!LooksLikeOutboxAction(action))
                    continue;

                var route = Provider.TryGetRouteBySuccessAction(action);
                if (route is null)
                    missing.Add($"{transition.Code}:{action}");
            }
        }

        Assert.Empty(missing);
    }

    private static bool LooksLikeOutboxAction(string action)
    {
        // Heuristic: any SuccessAction that starts with "CreateOutbox",
        // "SendTo", "Start", "CreateTreatment", or "UpdateProgress"
        // may be an outbox action. This list should stay in sync with
        // the provider's known action set.
        return action.StartsWith("CreateOutbox", StringComparison.Ordinal)
            || action is "SendToMonacoImport"
                or "StartScheduleWatch"
                or "CreateTreatmentMonitor"
                or "UpdateProgress";
    }
}
