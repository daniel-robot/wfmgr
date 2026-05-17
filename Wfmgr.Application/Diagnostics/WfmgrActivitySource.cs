using System.Diagnostics;
using System.Reflection;

namespace Wfmgr.Application.Diagnostics;

/// <summary>
/// Single <see cref="ActivitySource"/> for the wfmgr workflow engine. Exposes well-known
/// activity and tag names so producers and consumers stay consistent without a string-grep
/// hunt.
/// <para>
/// To consume these spans, register the source name with OpenTelemetry:
/// <c>builder.Services.AddOpenTelemetry().WithTracing(t =&gt; t.AddSource(WfmgrActivitySource.Name));</c>
/// </para>
/// </summary>
public static class WfmgrActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Wfmgr.Workflow";

    /// <summary>Shared <see cref="ActivitySource"/>; safe to use from any layer.</summary>
    public static readonly ActivitySource Source = new(
        Name,
        typeof(WfmgrActivitySource).Assembly.GetName().Version?.ToString() ?? "1.0.0");

    // ── Activity / span names ────────────────────────────────────────────────

    public const string ApplyTransition = "wfmgr.transition.apply";
    public const string GateValidate = "wfmgr.transition.gate-validate";
    public const string SideEffect = "wfmgr.transition.side-effect";
    public const string OutboxDeliver = "wfmgr.outbox.deliver";
    public const string ExternalEventReceive = "wfmgr.external-event.receive";

    // ── Tag keys ─────────────────────────────────────────────────────────────

    public const string TagCaseId = "wfmgr.case.id";
    public const string TagTriggerName = "wfmgr.trigger.name";
    public const string TagTransitionCode = "wfmgr.transition.code";
    public const string TagFromStatus = "wfmgr.transition.from";
    public const string TagToStatus = "wfmgr.transition.to";
    public const string TagResult = "wfmgr.transition.result";
    public const string TagGateCheck = "wfmgr.gate.check";
    public const string TagGateFailures = "wfmgr.gate.failures";
    public const string TagOutboxAction = "wfmgr.outbox.action";
    public const string TagOutboxTarget = "wfmgr.outbox.target";
    public const string TagOutboxDeliveryMode = "wfmgr.outbox.delivery-mode";
    public const string TagOutboxAttempt = "wfmgr.outbox.attempt";
    public const string TagOutboxMessageType = "wfmgr.outbox.message-type";
    public const string TagExternalSource = "wfmgr.external-event.source";
    public const string TagExternalType = "wfmgr.external-event.type";
    public const string TagExternalEventId = "wfmgr.external-event.id";
    public const string TagDuplicate = "wfmgr.external-event.duplicate";

    /// <summary>
    /// Returns the current activity's W3C <c>traceparent</c> header value, or <c>null</c>
    /// when there is no ambient activity / parent.
    /// </summary>
    public static string? CurrentTraceparent()
    {
        var current = Activity.Current;
        return current is null || current.IdFormat != ActivityIdFormat.W3C
            ? null
            : current.Id;
    }
}
