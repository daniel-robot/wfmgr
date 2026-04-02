namespace Wfmgr.Application.Workflows.V1.Compensation;

/// <summary>
/// Applies formal compensation for a failed workflow step using
/// <see cref="WorkflowCompensationCatalog"/> metadata.
/// <para>
/// A compensation lookup is by <paramref name="failedStepCode"/> (e.g. "IMG-002", "RX-006").
/// When a matching <c>CompensationDefinition</c> is found the service:
/// <list type="number">
///   <item>Optionally transitions the case to <c>TargetStatus</c> via
///   <c>ICaseTransitionService</c> (only when the status actually changes).</item>
///   <item>Creates the configured <c>WorkItemToCreate</c> if one is declared and no open
///   item of that type already exists.</item>
///   <item>Writes an <c>AuditLog</c> entry.</item>
///   <item>Writes a <c>CaseTransitionHistory</c> row when the status changed.</item>
///   <item>If a <c>RetryPolicy</c> is present and the retry budget is not exhausted,
///   enqueues an outbox retry message.</item>
/// </list>
/// Does <b>not</b> call <c>SaveChangesAsync</c> — the caller owns the unit of work.
/// </para>
/// </summary>
public interface IWorkflowCompensationService
{
    /// <summary>
    /// Handles a failure for the step identified by <paramref name="failedStepCode"/>.
    /// </summary>
    /// <param name="caseId">The case that experienced the failure.</param>
    /// <param name="failedStepCode">
    /// Transition code of the failing step (e.g. "IMG-002", "RX-006").
    /// Use <c>WorkflowTransitionCatalog.ByCode</c> keys or the special value
    /// <c>"ANY_EXTERNAL_EVENT"</c> for idempotency-rejected events.
    /// </param>
    /// <param name="context">Failure details collected by the caller.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CompensationResult> HandleFailureAsync(
        Guid caseId,
        string failedStepCode,
        CompensationContext context,
        CancellationToken ct = default);
}
