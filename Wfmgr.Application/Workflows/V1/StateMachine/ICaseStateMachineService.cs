using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Domain.Enums;

namespace Wfmgr.Application.Workflows.V1.StateMachine;

public interface ICaseStateMachineService
{
    Task ApplyTransitionAsync(CaseData caseData, CaseStatus toStatus, TransitionExecutionContext context, CancellationToken ct);
}
