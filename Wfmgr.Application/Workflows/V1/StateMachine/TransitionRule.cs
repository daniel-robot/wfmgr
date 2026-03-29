using Wfmgr.Domain.Enums;
using Wfmgr.Application.Abstractions.Persistence.Models;

namespace Wfmgr.Application.Workflows.V1.StateMachine;

public class TransitionRule
{
    public required CaseStatus FromStatus { get; init; }
    public required CaseStatus ToStatus { get; init; }
    public required string TriggerName { get; init; }
    public required WorkflowTriggerType TriggerType { get; init; }
    public string? RequiredRole { get; init; }
    public IReadOnlyList<string> GateConditions { get; init; } = Array.Empty<string>();
    public string? FailurePath { get; init; }
    public Func<CaseData, TransitionExecutionContext, CancellationToken, Task>? SideEffectsAsync { get; init; }
}
