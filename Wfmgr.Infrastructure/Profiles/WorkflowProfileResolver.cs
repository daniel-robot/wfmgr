using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Infrastructure.Persistence;

namespace Wfmgr.Infrastructure.Profiles;

public class WorkflowProfileResolver : IWorkflowProfileResolver
{
    private readonly WfmgrDbContext _dbContext;

    public WorkflowProfileResolver(WfmgrDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<S1ContouringStrategy> ResolveS1ContouringStrategyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct)
    {
        var profile = await ResolveProfileAsync(hospitalId, siteId, departmentId, ct);
        if (profile is null)
        {
            return new S1ContouringStrategy();
        }

        var rule = await _dbContext.WorkflowRules
            .AsNoTracking()
            .Where(x => x.ProfileId == profile.ProfileId && x.SlotCode == "S1_CONTOURING_STRATEGY" && x.IsEnabled)
            .OrderBy(x => x.Priority)
            .FirstOrDefaultAsync(ct);

        if (rule is null || string.IsNullOrWhiteSpace(rule.ConfigJson))
        {
            return new S1ContouringStrategy();
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<S1ContouringStrategy>(rule.ConfigJson, options) ?? new S1ContouringStrategy();
    }

    private async Task<Persistence.Entities.WorkflowProfileEntity?> ResolveProfileAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct)
    {
        var profiles = _dbContext.WorkflowProfiles
            .AsNoTracking()
            .Where(x => x.IsActive)
            .AsQueryable();

        var level1 = await profiles
            .Where(x => x.HospitalId == hospitalId && x.SiteId == siteId && x.DepartmentId == departmentId)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);
        if (level1 is not null) return level1;

        var level2 = await profiles
            .Where(x => x.HospitalId == hospitalId && x.SiteId == siteId && x.DepartmentId == null)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);
        if (level2 is not null) return level2;

        var level3 = await profiles
            .Where(x => x.HospitalId == hospitalId && x.SiteId == null && x.DepartmentId == null)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);
        if (level3 is not null) return level3;

        return await profiles
            .Where(x => x.HospitalId == null && x.SiteId == null && x.DepartmentId == null)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);
    }
}
