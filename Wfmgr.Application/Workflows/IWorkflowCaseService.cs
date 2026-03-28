namespace Wfmgr.Application.Workflows;

public interface IWorkflowCaseService
{
    Task<IReadOnlyList<WorkflowCaseDto>> GetActiveCasesAsync(CancellationToken cancellationToken = default);
}
