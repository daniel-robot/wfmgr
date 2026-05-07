using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Config;
using Wfmgr.Infrastructure.Persistence;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Profiles;

public class WorkflowConfigService : IWorkflowConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly WfmgrDbContext _dbContext;

    public WorkflowConfigService(WfmgrDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<WorkflowProfileDto>> GetProfilesAsync(CancellationToken ct)
    {
        var profiles = await _dbContext.WorkflowProfiles
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.HospitalId)
            .ThenBy(x => x.SiteId)
            .ThenBy(x => x.DepartmentId)
            .ThenByDescending(x => x.Version)
            .ToListAsync(ct);

        return profiles.Select(ToDto).ToList();
    }

    public async Task<WorkflowProfileDetailDto?> GetProfileAsync(Guid profileId, CancellationToken ct)
    {
        var profile = await _dbContext.WorkflowProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProfileId == profileId, ct);
        if (profile is null)
        {
            return null;
        }

        var rules = await _dbContext.WorkflowRules
            .AsNoTracking()
            .Where(x => x.ProfileId == profileId)
            .OrderBy(x => x.SlotCode)
            .ThenByDescending(x => x.Priority)
            .ToListAsync(ct);

        return new WorkflowProfileDetailDto(ToDto(profile), rules.Select(ToDto).ToList());
    }

    public async Task<WorkflowProfileDto> CreateProfileAsync(CreateWorkflowProfileRequest request, CancellationToken ct)
    {
        var entity = new WorkflowProfileEntity
        {
            ProfileId = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Version = request.Version,
            HospitalId = Normalize(request.HospitalId),
            SiteId = Normalize(request.SiteId),
            DepartmentId = Normalize(request.DepartmentId),
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _dbContext.WorkflowProfiles.AddAsync(entity, ct);
        await _dbContext.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task<WorkflowProfileDto?> UpdateProfileAsync(Guid profileId, UpdateWorkflowProfileRequest request, CancellationToken ct)
    {
        var entity = await _dbContext.WorkflowProfiles.FirstOrDefaultAsync(x => x.ProfileId == profileId, ct);
        if (entity is null)
        {
            return null;
        }

        if (request.Name is not null)
        {
            entity.Name = request.Name.Trim();
        }

        if (request.Version.HasValue)
        {
            entity.Version = request.Version.Value;
        }

        if (request.HospitalId is not null)
        {
            entity.HospitalId = Normalize(request.HospitalId);
        }

        if (request.SiteId is not null)
        {
            entity.SiteId = Normalize(request.SiteId);
        }

        if (request.DepartmentId is not null)
        {
            entity.DepartmentId = Normalize(request.DepartmentId);
        }

        if (request.IsActive.HasValue)
        {
            entity.IsActive = request.IsActive.Value;
        }

        await _dbContext.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<WorkflowProfileDto?> SetProfileActiveAsync(Guid profileId, bool isActive, CancellationToken ct)
    {
        var entity = await _dbContext.WorkflowProfiles.FirstOrDefaultAsync(x => x.ProfileId == profileId, ct);
        if (entity is null)
        {
            return null;
        }

        entity.IsActive = isActive;
        await _dbContext.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<WorkflowRuleDto>> GetRulesAsync(Guid profileId, string? slotCode, bool? enabled, CancellationToken ct)
    {
        var query = _dbContext.WorkflowRules
            .AsNoTracking()
            .Where(x => x.ProfileId == profileId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(slotCode))
        {
            query = query.Where(x => x.SlotCode == slotCode);
        }

        if (enabled.HasValue)
        {
            query = query.Where(x => x.IsEnabled == enabled.Value);
        }

        var rules = await query
            .OrderBy(x => x.SlotCode)
            .ThenByDescending(x => x.Priority)
            .ToListAsync(ct);

        return rules.Select(ToDto).ToList();
    }

    public async Task<WorkflowRuleDto?> GetRuleAsync(Guid ruleId, CancellationToken ct)
    {
        var entity = await _dbContext.WorkflowRules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RuleId == ruleId, ct);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<WorkflowRuleDto?> CreateRuleAsync(Guid profileId, CreateWorkflowRuleRequest request, CancellationToken ct)
    {
        var profileExists = await _dbContext.WorkflowProfiles.AnyAsync(x => x.ProfileId == profileId, ct);
        if (!profileExists)
        {
            return null;
        }

        var entity = new WorkflowRuleEntity
        {
            RuleId = Guid.NewGuid(),
            ProfileId = profileId,
            SlotCode = request.SlotCode.Trim(),
            Priority = request.Priority,
            IsEnabled = request.Enabled,
            ConditionJson = NormalizeJsonText(request.ConditionJson),
            ConfigJson = request.ConfigJson,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
        };

        await _dbContext.WorkflowRules.AddAsync(entity, ct);
        await _dbContext.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task<WorkflowRuleDto?> UpdateRuleAsync(Guid ruleId, UpdateWorkflowRuleRequest request, CancellationToken ct)
    {
        var entity = await _dbContext.WorkflowRules.FirstOrDefaultAsync(x => x.RuleId == ruleId, ct);
        if (entity is null)
        {
            return null;
        }

        entity.SlotCode = request.SlotCode.Trim();
        entity.Priority = request.Priority;
        entity.IsEnabled = request.Enabled;
        entity.ConditionJson = NormalizeJsonText(request.ConditionJson);
        entity.ConfigJson = request.ConfigJson;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;

        await _dbContext.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<WorkflowRuleDto?> SetRuleEnabledAsync(Guid ruleId, bool enabled, CancellationToken ct)
    {
        var entity = await _dbContext.WorkflowRules.FirstOrDefaultAsync(x => x.RuleId == ruleId, ct);
        if (entity is null)
        {
            return null;
        }

        entity.IsEnabled = enabled;
        await _dbContext.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public Task<ValidateWorkflowRuleResponse> ValidateRuleAsync(ValidateWorkflowRuleRequest request, CancellationToken ct)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(request.SlotCode))
        {
            errors.Add("slotCode is required.");
        }

        if (request.Priority.HasValue && request.Priority.Value < 0)
        {
            errors.Add("priority must be greater than or equal to 0.");
        }

        if (request.EffectiveFrom.HasValue && request.EffectiveTo.HasValue && request.EffectiveTo.Value < request.EffectiveFrom.Value)
        {
            errors.Add("effectiveTo cannot be earlier than effectiveFrom.");
        }

        if (string.IsNullOrWhiteSpace(request.ConfigJson))
        {
            errors.Add("configJson is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.ConditionJson) && !IsValidJson(request.ConditionJson, out var conditionError))
        {
            errors.Add($"conditionJson is not valid JSON: {conditionError}");
        }

        if (!string.IsNullOrWhiteSpace(request.ConditionJson))
        {
            warnings.Add("conditionJson is stored but is not currently interpreted by the runtime profile resolver.");
        }

        if (!GetSlotCodeSet().Contains(request.SlotCode))
        {
            errors.Add($"slotCode '{request.SlotCode}' is not supported.");
        }

        if (!string.IsNullOrWhiteSpace(request.ConfigJson))
        {
            if (!IsValidJson(request.ConfigJson, out var configError))
            {
                errors.Add($"configJson is not valid JSON: {configError}");
            }
            else if (errors.Count == 0)
            {
                var parsedConfig = TryParseSlotConfig(request.SlotCode, request.ConfigJson, out var parseError);
                if (parsedConfig is null)
                {
                    errors.Add(parseError ?? "configJson could not be parsed for slotCode.");
                }
                else
                {
                    var slotErrors = WorkflowSlotConfigValidator.Validate(request.SlotCode, parsedConfig);
                    errors.AddRange(slotErrors);
                }
            }
        }

        return Task.FromResult(new ValidateWorkflowRuleResponse(errors.Count == 0, errors, warnings));
    }

    public IReadOnlyList<WorkflowSlotCodeDto> GetSlotCodes()
    {
        return new[]
        {
            new WorkflowSlotCodeDto(WorkflowSlotCodes.S1ContouringStrategy, "S1 Contouring Strategy", "Auto contour provider and fallback behavior."),
            new WorkflowSlotCodeDto(WorkflowSlotCodes.S2ContourReviewPolicy, "S2 Contour Review Policy", "Review mode, rejection behavior, and timeout."),
            new WorkflowSlotCodeDto(WorkflowSlotCodes.S3PlanDispatch, "S3 Plan Dispatch", "Assignment mode and escalation policy for planning."),
            new WorkflowSlotCodeDto(WorkflowSlotCodes.S4PlanReReviewPolicy, "S4 Plan Re-review Policy", "Optional re-review trigger and reviewer role."),
            new WorkflowSlotCodeDto(WorkflowSlotCodes.S5PlanDoubleCheck, "S5 Plan Double Check", "Optional independent double-check policy."),
            new WorkflowSlotCodeDto(WorkflowSlotCodes.S6QueueAndCancelPolicy, "S6 Queue and Cancel Policy", "Queue mode and cancellation constraints."),
            new WorkflowSlotCodeDto(WorkflowSlotCodes.S7TreatmentCompletionPolicy, "S7 Treatment Completion Policy", "Completion mode and mismatch handling."),
            new WorkflowSlotCodeDto(WorkflowSlotCodes.S8ExceptionHandlingPolicy, "S8 Exception Handling Policy", "Retry strategy, fallback work item, and notifications."),
        };
    }

    public async Task<EffectiveWorkflowConfigDto> GetEffectiveConfigAsync(string? hospitalId, string? siteId, string? departmentId, CancellationToken ct)
    {
        var resolvedHospital = Normalize(hospitalId) ?? string.Empty;
        var resolvedSite = Normalize(siteId) ?? string.Empty;
        var resolvedDepartment = Normalize(departmentId) ?? string.Empty;

        var profile = await ResolveProfileAsync(resolvedHospital, resolvedSite, resolvedDepartment, ct);
        if (profile is null)
        {
            var emptySlots = GetSlotCodes().Select(x => new EffectiveWorkflowSlotDto(x.Code, null, null, null, null)).ToList();
            return new EffectiveWorkflowConfigDto(null, null, null, emptySlots);
        }

        var now = DateTimeOffset.UtcNow;
        var rules = await _dbContext.WorkflowRules
            .AsNoTracking()
            .Where(x => x.ProfileId == profile.ProfileId
                        && x.IsEnabled
                        && (x.EffectiveFrom == null || x.EffectiveFrom <= now)
                        && (x.EffectiveTo == null || x.EffectiveTo >= now))
            .OrderByDescending(x => x.Priority)
            .ToListAsync(ct);

        var resolvedSlots = GetSlotCodes()
            .Select(slot =>
            {
                var rule = rules.FirstOrDefault(x => x.SlotCode == slot.Code);
                return new EffectiveWorkflowSlotDto(
                    slot.Code,
                    profile.ProfileId,
                    rule?.RuleId,
                    rule?.Priority,
                    rule?.ConfigJson);
            })
            .ToList();

        return new EffectiveWorkflowConfigDto(
            profile.ProfileId,
            BuildProfileKey(profile),
            profile.Version,
            resolvedSlots);
    }

    private async Task<WorkflowProfileEntity?> ResolveProfileAsync(string hospitalId, string siteId, string departmentId, CancellationToken ct)
    {
        var profiles = _dbContext.WorkflowProfiles
            .AsNoTracking()
            .Where(x => x.IsActive)
            .AsQueryable();

        var level1 = await profiles
            .Where(x => x.HospitalId == hospitalId && x.SiteId == siteId && x.DepartmentId == departmentId)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);
        if (level1 is not null)
        {
            return level1;
        }

        var level2 = await profiles
            .Where(x => x.HospitalId == hospitalId && x.SiteId == siteId && x.DepartmentId == null)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);
        if (level2 is not null)
        {
            return level2;
        }

        var level3 = await profiles
            .Where(x => x.HospitalId == hospitalId && x.SiteId == null && x.DepartmentId == null)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);
        if (level3 is not null)
        {
            return level3;
        }

        return await profiles
            .Where(x => x.HospitalId == null && x.SiteId == null && x.DepartmentId == null)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(ct);
    }

    private static object? TryParseSlotConfig(string slotCode, string json, out string? error)
    {
        try
        {
            error = null;
            return slotCode switch
            {
                WorkflowSlotCodes.S1ContouringStrategy => JsonSerializer.Deserialize<S1ContouringStrategy>(json, JsonOptions),
                WorkflowSlotCodes.S2ContourReviewPolicy => JsonSerializer.Deserialize<S2ContourReviewPolicy>(json, JsonOptions),
                WorkflowSlotCodes.S3PlanDispatch => JsonSerializer.Deserialize<S3PlanDispatchPolicy>(json, JsonOptions),
                WorkflowSlotCodes.S4PlanReReviewPolicy => JsonSerializer.Deserialize<S4PlanReReviewPolicy>(json, JsonOptions),
                WorkflowSlotCodes.S5PlanDoubleCheck => JsonSerializer.Deserialize<S5PlanDoubleCheckPolicy>(json, JsonOptions),
                WorkflowSlotCodes.S6QueueAndCancelPolicy => JsonSerializer.Deserialize<S6QueueAndCancelPolicy>(json, JsonOptions),
                WorkflowSlotCodes.S7TreatmentCompletionPolicy => JsonSerializer.Deserialize<S7TreatmentCompletionPolicy>(json, JsonOptions),
                WorkflowSlotCodes.S8ExceptionHandlingPolicy => JsonSerializer.Deserialize<S8ExceptionHandlingPolicy>(json, JsonOptions),
                _ => null
            };
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    private static bool IsValidJson(string json, out string? error)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static HashSet<string> GetSlotCodeSet()
    {
        return new HashSet<string>(
            new[]
            {
                WorkflowSlotCodes.S1ContouringStrategy,
                WorkflowSlotCodes.S2ContourReviewPolicy,
                WorkflowSlotCodes.S3PlanDispatch,
                WorkflowSlotCodes.S4PlanReReviewPolicy,
                WorkflowSlotCodes.S5PlanDoubleCheck,
                WorkflowSlotCodes.S6QueueAndCancelPolicy,
                WorkflowSlotCodes.S7TreatmentCompletionPolicy,
                WorkflowSlotCodes.S8ExceptionHandlingPolicy,
            },
            StringComparer.Ordinal);
    }

    private static WorkflowProfileDto ToDto(WorkflowProfileEntity entity)
    {
        return new WorkflowProfileDto(
            entity.ProfileId,
            BuildProfileKey(entity),
            entity.Name,
            entity.Version,
            entity.HospitalId,
            entity.SiteId,
            entity.DepartmentId,
            entity.IsActive,
            entity.CreatedAt,
            null);
    }

    private static WorkflowRuleDto ToDto(WorkflowRuleEntity entity)
    {
        return new WorkflowRuleDto(
            entity.RuleId,
            entity.ProfileId,
            entity.SlotCode,
            entity.Priority,
            entity.IsEnabled,
            entity.ConditionJson,
            entity.ConfigJson,
            entity.EffectiveFrom,
            entity.EffectiveTo,
            null,
            null);
    }

    private static string BuildProfileKey(WorkflowProfileEntity entity)
    {
        var hospital = string.IsNullOrWhiteSpace(entity.HospitalId) ? "*" : entity.HospitalId;
        var site = string.IsNullOrWhiteSpace(entity.SiteId) ? "*" : entity.SiteId;
        var department = string.IsNullOrWhiteSpace(entity.DepartmentId) ? "*" : entity.DepartmentId;
        return $"{hospital}:{site}:{department}:v{entity.Version}";
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string? NormalizeJsonText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value;
    }
}
