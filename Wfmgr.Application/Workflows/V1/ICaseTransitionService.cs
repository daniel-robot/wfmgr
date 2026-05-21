using Wfmgr.Application.Workflows.V1.StateMachine;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Application.Workflows.V1.Gates;
using Wfmgr.Domain.Enums;

namespace Wfmgr.Application.Workflows.V1;

/// <summary>
/// Central service for executing workflow transitions on a radiotherapy case.
/// <para>
/// For each call the service:
/// <list type="number">
///   <item>Looks up the matching <see cref="Definitions.TransitionDefinition"/> in
///     <see cref="WorkflowTransitionCatalog"/> by trigger name and current case status.</item>
///   <item>Validates <see cref="Definitions.TransitionDefinition.RequiredRoles"/> against the
///     actor roles in the supplied <see cref="GateValidationContext"/>.</item>
///   <item>Invokes <see cref="IGateValidationService"/> to evaluate all named gate checks.</item>
///   <item>On success: mutates <see cref="CaseData.CurrentStatus"/>, increments
///     <see cref="CaseData.StatusVersion"/>, and writes an <c>AuditLog</c> and
///     <c>CaseTransitionHistory</c> record.</item>
///   <item>On failure: returns a structured <see cref="TransitionExecutionResult"/> describing
///     why the transition was rejected without mutating any state.</item>
/// </list>
/// </para>
/// <para>
/// The service intentionally does <b>not</b> call
/// <c>IWorkflowDataAccess.SaveChangesAsync</c>; the caller is responsible for flushing the
/// unit of work once all side-effects (work items, outbox messages, etc.) have been staged.
/// </para>
/// </summary>
public interface ICaseTransitionService
{
    /// <summary>
    /// Applies the transition to a generic workflow subject.
    /// </summary>
    Task<TransitionExecutionResult> ApplyTransitionAsync(
        IWorkflowSubject subject,
        string triggerName,
        GateValidationContext context,
        CancellationToken ct = default,
        CaseStatus? fallbackToStatus = null);
    /// <summary>
    /// Loads the case identified by <paramref name="caseId"/> then applies the transition
    /// named <paramref name="triggerName"/>.
    /// </summary>
    /// <param name="caseId">ID of the case to transition.</param>
    /// <param name="triggerName">
    /// Trigger name matching a <see cref="Definitions.TransitionDefinition.TriggerName"/>
    /// in <see cref="WorkflowTransitionCatalog"/>.
    /// </param>
    /// <param name="context">Execution context with actor identity, roles, and supplementary metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="fallbackToStatus">
    /// Target status to use when no catalog entry is found for <paramref name="triggerName"/>
    /// and the current case status.  Providing this value enables backward-compatible operation
    /// for transition trigger names not yet represented in the catalog (gate checks are skipped).
    /// Pass <c>null</c> to require a catalog match.
    /// </param>
    /// <returns>
    /// A <see cref="TransitionExecutionResult"/> that is successful when the transition was applied,
    /// or contains failure details without mutating any state.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the case is not found.</exception>
    Task<TransitionExecutionResult> ApplyTransitionAsync(
        Guid caseId,
        string triggerName,
        GateValidationContext context,
        CancellationToken ct = default,
        CaseStatus? fallbackToStatus = null);

    /// <summary>
    /// Applies the transition named <paramref name="triggerName"/> to a pre-loaded
    /// <paramref name="caseData"/> instance.
    /// <para>
    /// Use this overload when the caller has already loaded and possibly mutated the
    /// <see cref="CaseData"/> before the transition (e.g. assigning image references) and the
    /// same in-memory object must be updated in place.
    /// </para>
    /// </summary>
    /// <param name="caseData">
    /// Mutable, tracked <see cref="CaseData"/> obtained from
    /// <c>IWorkflowDataAccess.GetCaseByIdAsync</c>.  The object's status and version fields are
    /// updated in place on success.
    /// </param>
    /// <param name="triggerName">Trigger name to look up in the transition catalog.</param>
    /// <param name="context">Execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="fallbackToStatus">
    /// Fallback target status when no catalog entry is found.  See the primary overload for details.
    /// </param>
    Task<TransitionExecutionResult> ApplyTransitionAsync(
        CaseData caseData,
        string triggerName,
        GateValidationContext context,
        CancellationToken ct = default,
        CaseStatus? fallbackToStatus = null);
}
