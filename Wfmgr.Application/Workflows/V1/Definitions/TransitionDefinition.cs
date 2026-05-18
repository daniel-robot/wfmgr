using Wfmgr.Domain.Enums;

namespace Wfmgr.Application.Workflows.V1.Definitions;

/// <summary>
/// Static catalog record that describes a single named transition in the radiotherapy
/// workflow state machine.
/// <para>
/// A <see cref="TransitionDefinition"/> captures everything needed to understand <em>what</em>
/// a transition does and <em>when</em> it is allowed — source statuses, target status, trigger
/// semantics, role requirements, gate-check names, and the side-effect hints (success/failure
/// actions and work items to create).
/// </para>
/// <para>
/// These objects are pure metadata used for catalog lookup, documentation, and bootstrapping
/// the state machine.  Actual execution logic lives in the state-machine service layer.
/// </para>
/// </summary>
public sealed class TransitionDefinition
{
    /// <summary>Unique business code that identifies this transition (e.g. "SIM-001").</summary>
    public required string Code { get; init; }

    /// <summary>
    /// One or more source <see cref="CaseStatus"/> values from which this transition is valid.
    /// Most transitions have a single source; cancel and rework transitions may declare several.
    /// </summary>
    public required CaseStatus[] FromStatuses { get; init; }

    /// <summary>The <see cref="CaseStatus"/> reached when the transition succeeds.</summary>
    public required CaseStatus ToStatus { get; init; }

    /// <summary>Name of the command or event that initiates the transition (e.g. "SubmitSimulationRequest").</summary>
    public required string TriggerName { get; init; }

    /// <summary>Category of actor that fires the trigger.</summary>
    public required WorkflowTriggerType TriggerType { get; init; }

    /// <summary>
    /// Roles that the actor may hold to perform this transition.
    /// An empty list means the transition is not role-gated (typically system-initiated transitions).
    /// When multiple roles are listed, holding any one of them is sufficient.
    /// </summary>
    public IReadOnlyList<string> RequiredRoles { get; init; } = [];

    /// <summary>
    /// Named gate-check identifiers that must pass before the transition is allowed.
    /// Each string is a symbolic name resolved by <c>IGateValidationService</c>.
    /// </summary>
    public string[] GateChecks { get; init; } = [];

    /// <summary>
    /// Named side-effect descriptors executed when the transition succeeds
    /// (e.g. "SaveForm", "AuditTransitionHistory").
    /// </summary>
    public string[] SuccessActions { get; init; } = [];

    /// <summary>
    /// Named actions taken when a gate check fails and the transition is rejected
    /// (e.g. "RejectTransition", "StayInCurrentStatus").
    /// </summary>
    public string[] FailureActions { get; init; } = [];

    /// <summary>
    /// <see cref="Domain.WorkItems.WorkItemTypes"/> names of work items that should be
    /// created as a result of a successful transition.
    /// </summary>
    public string[] WorkItemsToCreate { get; init; } = [];

    /// <summary>
    /// Workflow configuration slot code (from <c>WorkflowSlotCodes</c>) that governs optional
    /// behaviour associated with this transition.  <c>null</c> means the transition is
    /// unconditional with respect to site/department configuration.
    /// </summary>
    public string? ConfigSlot { get; init; }
}
