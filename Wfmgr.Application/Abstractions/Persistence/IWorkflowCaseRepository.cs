using Wfmgr.Domain.Entities;

namespace Wfmgr.Application.Abstractions.Persistence;

public interface IWorkflowCaseRepository
{
    Task<IReadOnlyList<WorkflowCase>> GetActiveAsync(CancellationToken cancellationToken = default);
}
