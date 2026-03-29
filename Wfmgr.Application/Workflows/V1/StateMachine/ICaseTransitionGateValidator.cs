using Wfmgr.Application.Abstractions.Persistence.Models;

namespace Wfmgr.Application.Workflows.V1.StateMachine;

public interface ICaseTransitionGateValidator
{
    Task ValidateAsync(CaseData caseData, TransitionRule rule, TransitionExecutionContext context, CancellationToken ct);
}
