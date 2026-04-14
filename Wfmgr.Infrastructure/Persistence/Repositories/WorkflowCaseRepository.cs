using Microsoft.EntityFrameworkCore;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Domain.Entities;

namespace Wfmgr.Infrastructure.Persistence.Repositories;

public class WorkflowCaseRepository : IWorkflowCaseRepository
{
    private readonly WfmgrDbContext _dbContext;

    public WorkflowCaseRepository(WfmgrDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<WorkflowCase>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var caseEntities = await _dbContext.Cases
            .AsNoTracking()
            .Where(x => x.CurrentStatus != Domain.Enums.CaseStatus.Cancelled)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return caseEntities
            .Select(x => new WorkflowCase
            {
                Id = x.CaseId,
                PatientId = x.PatientId ?? string.Empty,
                PlanId = x.AccessionNumber,
                Status = x.CurrentStatus.ToString(),
                CreatedAtUtc = x.CreatedAt
            })
            .ToList();
    }
}
