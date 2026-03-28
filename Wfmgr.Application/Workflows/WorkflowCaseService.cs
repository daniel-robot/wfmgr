using Wfmgr.Application.Abstractions.Persistence;

namespace Wfmgr.Application.Workflows;

public class WorkflowCaseService : IWorkflowCaseService
{
    private readonly IWorkflowCaseRepository _workflowCaseRepository;

    public WorkflowCaseService(IWorkflowCaseRepository workflowCaseRepository)
    {
        _workflowCaseRepository = workflowCaseRepository;
    }

    public async Task<IReadOnlyList<WorkflowCaseDto>> GetActiveCasesAsync(CancellationToken cancellationToken = default)
    {
        var cases = await _workflowCaseRepository.GetActiveAsync(cancellationToken);

        return cases
            .Select(c => new WorkflowCaseDto(c.Id, c.PatientId, c.PlanId, c.Status, c.CreatedAtUtc))
            .ToList();
    }
}
