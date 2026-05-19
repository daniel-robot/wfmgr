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
        => await ResolveSlotConfigAsync(
            hospitalId,
            siteId,
            departmentId,
            WorkflowSlotCodes.S1ContouringStrategy,
            () => new S1ContouringStrategy(),
            ct);

    public async Task<S2ContourReviewPolicy> ResolveS2ContourReviewPolicyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct)
        => await ResolveSlotConfigAsync(
            hospitalId,
            siteId,
            departmentId,
            WorkflowSlotCodes.S2ContourReviewPolicy,
            () => new S2ContourReviewPolicy(),
            ct);

    public async Task<S3PlanDispatchPolicy> ResolveS3PlanDispatchPolicyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct)
        => await ResolveSlotConfigAsync(
            hospitalId,
            siteId,
            departmentId,
            WorkflowSlotCodes.S3PlanDispatch,
            () => new S3PlanDispatchPolicy(),
            ct);

    public async Task<S4PlanReReviewPolicy> ResolveS4PlanReReviewPolicyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct)
        => await ResolveSlotConfigAsync(
            hospitalId,
            siteId,
            departmentId,
            WorkflowSlotCodes.S4PlanReReviewPolicy,
            () => new S4PlanReReviewPolicy(),
            ct);

    public async Task<S5PlanDoubleCheckPolicy> ResolveS5PlanDoubleCheckPolicyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct)
        => await ResolveSlotConfigAsync(
            hospitalId,
            siteId,
            departmentId,
            WorkflowSlotCodes.S5PlanDoubleCheck,
            () => new S5PlanDoubleCheckPolicy(),
            ct);

    public async Task<S6CancelPolicy> ResolveS6CancelPolicyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct)
        => await ResolveSlotConfigAsync(
            hospitalId,
            siteId,
            departmentId,
            WorkflowSlotCodes.S6CancelPolicy,
            () => new S6CancelPolicy(),
            ct);

    public async Task<S7TreatmentCompletionPolicy> ResolveS7TreatmentCompletionPolicyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct)
        => await ResolveSlotConfigAsync(
            hospitalId,
            siteId,
            departmentId,
            WorkflowSlotCodes.S7TreatmentCompletionPolicy,
            () => new S7TreatmentCompletionPolicy(),
            ct);

    public async Task<S8ExceptionHandlingPolicy> ResolveS8ExceptionHandlingPolicyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct)
        => await ResolveSlotConfigAsync(
            hospitalId,
            siteId,
            departmentId,
            WorkflowSlotCodes.S8ExceptionHandlingPolicy,
            () => new S8ExceptionHandlingPolicy(),
            ct);

    private async Task<TConfig> ResolveSlotConfigAsync<TConfig>(
        string hospitalId,
        string siteId,
        string departmentId,
        string slotCode,
        Func<TConfig> fallbackFactory,
        CancellationToken ct)
        where TConfig : class
    {
        var profile = await ResolveProfileAsync(hospitalId, siteId, departmentId, ct);
        if (profile is null)
        {
            return fallbackFactory();
        }

        var now = DateTimeOffset.UtcNow;
        var rule = await _dbContext.WorkflowRules
            .AsNoTracking()
            .Where(x =>
                x.ProfileId == profile.ProfileId &&
                x.SlotCode == slotCode &&
                x.IsEnabled &&
                (x.EffectiveFrom == null || x.EffectiveFrom <= now) &&
                (x.EffectiveTo == null || x.EffectiveTo >= now))
            .OrderByDescending(x => x.Priority)
            .FirstOrDefaultAsync(ct);

        if (rule is null || string.IsNullOrWhiteSpace(rule.ConfigJson))
        {
            return fallbackFactory();
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config = JsonSerializer.Deserialize<TConfig>(rule.ConfigJson, options);
        if (config is null)
        {
            return fallbackFactory();
        }

        var validationErrors = WorkflowSlotConfigValidator.Validate(slotCode, config);
        return validationErrors.Count == 0 ? config : fallbackFactory();
    }

    private async Task<Persistence.Entities.WorkflowProfileEntity?> ResolveProfileAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct)
    {
        // Mirrors database/init.sql so a freshly-migrated empty DB still has the
        // two default profiles + 8 default slot rules. No-op once seeded.
        await WorkflowProfileSeeder.EnsureSeededAsync(_dbContext, ct);

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
