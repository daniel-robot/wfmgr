using System.Security.Cryptography;
using System.Text;
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

    public async Task<WorkflowProfileDto> CreateProfileAsync(CreateWorkflowProfileRequest request, string? actorId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new WorkflowProfileEntity
        {
            ProfileId = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Version = request.Version,
            HospitalId = Normalize(request.HospitalId),
            SiteId = Normalize(request.SiteId),
            DepartmentId = Normalize(request.DepartmentId),
            IsActive = request.IsActive,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId,
        };

        await _dbContext.WorkflowProfiles.AddAsync(entity, ct);
        WriteChangeLog("Profile", entity.ProfileId, entity.ProfileId, "Create", actorId, now, request.ChangeReason, SerializeProfileSnapshot(entity));
        await _dbContext.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task<WorkflowProfileMutationResult> UpdateProfileAsync(Guid profileId, UpdateWorkflowProfileRequest request, string? actorId, CancellationToken ct)
    {
        var entity = await _dbContext.WorkflowProfiles.FirstOrDefaultAsync(x => x.ProfileId == profileId, ct);
        if (entity is null)
        {
            return WorkflowProfileMutationResult.NotFoundResult();
        }

        var currentHash = ComputeProfileHash(entity);
        if (IsHashConflict(request.ExpectedHash, currentHash))
        {
            return WorkflowProfileMutationResult.ConflictResult(new WorkflowMutationConflictDto(
                "The profile was changed by another user. Reload before saving.",
                currentHash));
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

        var now = DateTimeOffset.UtcNow;
        entity.UpdatedAt = now;
        entity.UpdatedBy = actorId;

        WriteChangeLog("Profile", entity.ProfileId, entity.ProfileId, "Update", actorId, now, request.ChangeReason, SerializeProfileSnapshot(entity));

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return WorkflowProfileMutationResult.ConflictResult(new WorkflowMutationConflictDto(
                "The profile was modified concurrently. Reload before saving.",
                null));
        }

        return WorkflowProfileMutationResult.Success(ToDto(entity));
    }

    public async Task<WorkflowProfileMutationResult> SetProfileActiveAsync(Guid profileId, bool isActive, ToggleWorkflowProfileRequest request, string? actorId, CancellationToken ct)
    {
        var entity = await _dbContext.WorkflowProfiles.FirstOrDefaultAsync(x => x.ProfileId == profileId, ct);
        if (entity is null)
        {
            return WorkflowProfileMutationResult.NotFoundResult();
        }

        var currentHash = ComputeProfileHash(entity);
        if (IsHashConflict(request.ExpectedHash, currentHash))
        {
            return WorkflowProfileMutationResult.ConflictResult(new WorkflowMutationConflictDto(
                "The profile was changed by another user. Reload before saving.",
                currentHash));
        }

        entity.IsActive = isActive;
        var now = DateTimeOffset.UtcNow;
        entity.UpdatedAt = now;
        entity.UpdatedBy = actorId;

        WriteChangeLog("Profile", entity.ProfileId, entity.ProfileId, isActive ? "Activate" : "Deactivate", actorId, now, request.ChangeReason, SerializeProfileSnapshot(entity));

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return WorkflowProfileMutationResult.ConflictResult(new WorkflowMutationConflictDto(
                "The profile was modified concurrently. Reload before saving.",
                null));
        }

        return WorkflowProfileMutationResult.Success(ToDto(entity));
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

    public async Task<WorkflowRuleMutationResult> CreateRuleAsync(Guid profileId, CreateWorkflowRuleRequest request, string? actorId, CancellationToken ct)
    {
        var validation = await ValidateRuleAsync(new ValidateWorkflowRuleRequest(
            request.SlotCode,
            request.ConfigJson,
            request.ConditionJson,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.Priority), ct);

        if (!validation.IsValid)
        {
            return WorkflowRuleMutationResult.Invalid(validation);
        }

        var profileExists = await _dbContext.WorkflowProfiles.AnyAsync(x => x.ProfileId == profileId, ct);
        if (!profileExists)
        {
            return WorkflowRuleMutationResult.NotFoundResult();
        }

        var now = DateTimeOffset.UtcNow;
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
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId,
        };

        await _dbContext.WorkflowRules.AddAsync(entity, ct);
        WriteChangeLog("Rule", entity.RuleId, entity.ProfileId, "Create", actorId, now, request.ChangeReason, SerializeRuleSnapshot(entity));
        await _dbContext.SaveChangesAsync(ct);

        return WorkflowRuleMutationResult.Success(ToDto(entity));
    }

    public async Task<WorkflowRuleMutationResult> UpdateRuleAsync(Guid ruleId, UpdateWorkflowRuleRequest request, string? actorId, CancellationToken ct)
    {
        var validation = await ValidateRuleAsync(new ValidateWorkflowRuleRequest(
            request.SlotCode,
            request.ConfigJson,
            request.ConditionJson,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.Priority), ct);

        if (!validation.IsValid)
        {
            return WorkflowRuleMutationResult.Invalid(validation);
        }

        var entity = await _dbContext.WorkflowRules.FirstOrDefaultAsync(x => x.RuleId == ruleId, ct);
        if (entity is null)
        {
            return WorkflowRuleMutationResult.NotFoundResult();
        }

        var currentHash = ComputeRuleHash(entity);
        if (IsHashConflict(request.ExpectedHash, currentHash))
        {
            return WorkflowRuleMutationResult.ConflictResult(new WorkflowMutationConflictDto(
                "The rule was changed by another user. Reload before saving.",
                currentHash));
        }

        entity.SlotCode = request.SlotCode.Trim();
        entity.Priority = request.Priority;
        entity.IsEnabled = request.Enabled;
        entity.ConditionJson = NormalizeJsonText(request.ConditionJson);
        entity.ConfigJson = request.ConfigJson;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        var now = DateTimeOffset.UtcNow;
        entity.UpdatedAt = now;
        entity.UpdatedBy = actorId;

        WriteChangeLog("Rule", entity.RuleId, entity.ProfileId, "Update", actorId, now, request.ChangeReason, SerializeRuleSnapshot(entity));

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return WorkflowRuleMutationResult.ConflictResult(new WorkflowMutationConflictDto(
                "The rule was modified concurrently. Reload before saving.",
                null));
        }
        return WorkflowRuleMutationResult.Success(ToDto(entity));
    }

    public async Task<WorkflowRuleMutationResult> SetRuleEnabledAsync(Guid ruleId, bool enabled, ToggleWorkflowRuleRequest request, string? actorId, CancellationToken ct)
    {
        var entity = await _dbContext.WorkflowRules.FirstOrDefaultAsync(x => x.RuleId == ruleId, ct);
        if (entity is null)
        {
            return WorkflowRuleMutationResult.NotFoundResult();
        }

        var currentHash = ComputeRuleHash(entity);
        if (IsHashConflict(request.ExpectedHash, currentHash))
        {
            return WorkflowRuleMutationResult.ConflictResult(new WorkflowMutationConflictDto(
                "The rule was changed by another user. Reload before saving.",
                currentHash));
        }

        entity.IsEnabled = enabled;
        var now = DateTimeOffset.UtcNow;
        entity.UpdatedAt = now;
        entity.UpdatedBy = actorId;

        WriteChangeLog("Rule", entity.RuleId, entity.ProfileId, enabled ? "Enable" : "Disable", actorId, now, request.ChangeReason, SerializeRuleSnapshot(entity));

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return WorkflowRuleMutationResult.ConflictResult(new WorkflowMutationConflictDto(
                "The rule was modified concurrently. Reload before saving.",
                null));
        }
        return WorkflowRuleMutationResult.Success(ToDto(entity));
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
            new WorkflowSlotCodeDto(WorkflowSlotCodes.S6CancelPolicy, "S6 Cancel Policy", "Cancellation constraints and fallback behavior."),
            new WorkflowSlotCodeDto(WorkflowSlotCodes.S7TreatmentCompletionPolicy, "S7 Treatment Completion Policy", "Completion mode and mismatch handling."),
            new WorkflowSlotCodeDto(WorkflowSlotCodes.S8ExceptionHandlingPolicy, "S8 Exception Handling Policy", "Retry strategy, fallback work item, and notifications."),
        };
    }

    public async Task<EffectiveWorkflowConfigDto> GetEffectiveConfigAsync(string? hospitalId, string? siteId, string? departmentId, CancellationToken ct)
    {
        var queryHospital = Normalize(hospitalId);
        var querySite = Normalize(siteId);
        var queryDepartment = Normalize(departmentId);

        var allActiveProfiles = await _dbContext.WorkflowProfiles
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(ct);

        var candidates = allActiveProfiles
            .Select(p => new
            {
                Profile = p,
                Rank = GetProfileMatchRank(p, queryHospital, querySite, queryDepartment)
            })
            .OrderBy(x => x.Rank)
            .ThenByDescending(x => x.Profile.Version)
            .ThenBy(x => x.Profile.CreatedAt)
            .ToList();

        var matched = candidates.FirstOrDefault(x => x.Rank < int.MaxValue);

        var evaluatedProfiles = candidates.Select(x => new EffectiveWorkflowEvaluatedProfileDto(
            x.Profile.ProfileId,
            BuildProfileKey(x.Profile),
            x.Profile.Version,
            x.Profile.HospitalId,
            x.Profile.SiteId,
            x.Profile.DepartmentId,
            x.Profile.IsActive,
            x.Rank < int.MaxValue,
            x.Rank == int.MaxValue
                ? "Skipped: profile scope does not match query fallback chain."
                : matched is not null && matched.Profile.ProfileId == x.Profile.ProfileId
                    ? "Included: first matching profile by fallback priority and highest version."
                    : "Skipped: lower-priority matching profile."))
            .ToList();

        if (matched is null)
        {
            var unresolved = GetSlotCodes().Select(slot => new EffectiveWorkflowSlotDto(
                slot.Code,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "No active profile matched the requested scope."))
                .ToList();

            var unmatched = GetSlotCodes().Select(slot => new EffectiveWorkflowUnmatchedSlotDto(
                slot.Code,
                "No active profile matched the requested scope."))
                .ToList();

            return new EffectiveWorkflowConfigDto(
                new EffectiveWorkflowQueryDto(queryHospital, querySite, queryDepartment),
                null,
                unresolved,
                unmatched,
                evaluatedProfiles);
        }

        var now = DateTimeOffset.UtcNow;
        var activeRules = await _dbContext.WorkflowRules
            .AsNoTracking()
            .Where(x => x.ProfileId == matched.Profile.ProfileId
                        && x.IsEnabled
                        && (x.EffectiveFrom == null || x.EffectiveFrom <= now)
                        && (x.EffectiveTo == null || x.EffectiveTo >= now))
            .OrderByDescending(x => x.Priority)
            .ToListAsync(ct);

        var resolvedSlots = new List<EffectiveWorkflowSlotDto>();
        var unmatchedSlots = new List<EffectiveWorkflowUnmatchedSlotDto>();

        foreach (var slot in GetSlotCodes())
        {
            var rule = activeRules.FirstOrDefault(x => x.SlotCode == slot.Code);
            if (rule is null)
            {
                var reason = "No enabled/effective rule found in matched profile for this slot.";
                resolvedSlots.Add(new EffectiveWorkflowSlotDto(
                    slot.Code,
                    matched.Profile.ProfileId,
                    BuildProfileKey(matched.Profile),
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    reason));
                unmatchedSlots.Add(new EffectiveWorkflowUnmatchedSlotDto(slot.Code, reason));
                continue;
            }

            resolvedSlots.Add(new EffectiveWorkflowSlotDto(
                slot.Code,
                matched.Profile.ProfileId,
                BuildProfileKey(matched.Profile),
                rule.RuleId,
                rule.Priority,
                rule.IsEnabled,
                rule.EffectiveFrom,
                rule.EffectiveTo,
                rule.ConfigJson,
                "Resolved from matched profile and highest-priority active/effective rule."));
        }

        var matchedProfileDto = new EffectiveWorkflowMatchedProfileDto(
            matched.Profile.ProfileId,
            BuildProfileKey(matched.Profile),
            matched.Profile.Version,
            matched.Profile.HospitalId,
            matched.Profile.SiteId,
            matched.Profile.DepartmentId);

        return new EffectiveWorkflowConfigDto(
            new EffectiveWorkflowQueryDto(queryHospital, querySite, queryDepartment),
            matchedProfileDto,
            resolvedSlots,
            unmatchedSlots,
            evaluatedProfiles);
    }

    // TODO: Wire workflow configuration changes into a non-case-scoped audit pipeline before production.

    private static int GetProfileMatchRank(WorkflowProfileEntity profile, string? hospitalId, string? siteId, string? departmentId)
    {
        if (!string.Equals(profile.HospitalId, hospitalId, StringComparison.Ordinal)
            && profile.HospitalId is not null)
        {
            return int.MaxValue;
        }

        if (!string.Equals(profile.SiteId, siteId, StringComparison.Ordinal)
            && profile.SiteId is not null)
        {
            return int.MaxValue;
        }

        if (!string.Equals(profile.DepartmentId, departmentId, StringComparison.Ordinal)
            && profile.DepartmentId is not null)
        {
            return int.MaxValue;
        }

        if (profile.HospitalId == hospitalId && profile.SiteId == siteId && profile.DepartmentId == departmentId)
        {
            return 0;
        }

        if (profile.HospitalId == hospitalId && profile.SiteId == siteId && profile.DepartmentId is null)
        {
            return 1;
        }

        if (profile.HospitalId == hospitalId && profile.SiteId is null && profile.DepartmentId is null)
        {
            return 2;
        }

        if (profile.HospitalId is null && profile.SiteId is null && profile.DepartmentId is null)
        {
            return 3;
        }

        return int.MaxValue;
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
                WorkflowSlotCodes.S6CancelPolicy => JsonSerializer.Deserialize<S6CancelPolicy>(json, JsonOptions),
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
                WorkflowSlotCodes.S6CancelPolicy,
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
            ComputeProfileHash(entity),
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static WorkflowRuleDto ToDto(WorkflowRuleEntity entity)
    {
        return new WorkflowRuleDto(
            entity.RuleId,
            entity.ProfileId,
            entity.SlotCode,
            entity.Priority,
            entity.IsEnabled,
            ComputeRuleHash(entity),
            entity.ConditionJson,
            entity.ConfigJson,
            entity.EffectiveFrom,
            entity.EffectiveTo,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static string ComputeProfileHash(WorkflowProfileEntity entity)
    {
        return ComputeHash(
            entity.Name,
            entity.Version.ToString(),
            entity.HospitalId,
            entity.SiteId,
            entity.DepartmentId,
            entity.IsActive ? "1" : "0");
    }

    private static string ComputeRuleHash(WorkflowRuleEntity entity)
    {
        return ComputeHash(
            entity.SlotCode,
            entity.Priority.ToString(),
            entity.IsEnabled ? "1" : "0",
            entity.EffectiveFrom?.ToString("O"),
            entity.EffectiveTo?.ToString("O"),
            entity.ConditionJson,
            entity.ConfigJson);
    }

    private static string ComputeHash(params string?[] parts)
    {
        var payload = string.Join("|", parts.Select(x => x ?? string.Empty));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
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

    // ── Concurrency, audit log, and changelog helpers ─────────────────────────

    private static bool IsHashConflict(string? expectedHash, string currentHash)
    {
        return !string.IsNullOrWhiteSpace(expectedHash)
               && !string.Equals(expectedHash, currentHash, StringComparison.Ordinal);
    }

    private void WriteChangeLog(
        string entityType,
        Guid entityId,
        Guid profileId,
        string action,
        string? actorId,
        DateTimeOffset now,
        string? changeReason,
        string? snapshotJson)
    {
        _dbContext.WorkflowConfigChangeLogs.Add(new WorkflowConfigChangeLogEntity
        {
            EntityType = entityType,
            EntityId = entityId,
            ProfileId = profileId,
            Action = action,
            ActorId = actorId,
            CreatedAt = now,
            ChangeReason = changeReason,
            SnapshotJson = snapshotJson,
        });
    }

    private static string SerializeProfileSnapshot(WorkflowProfileEntity entity) => JsonSerializer.Serialize(new
    {
        entity.ProfileId,
        entity.Name,
        entity.Version,
        entity.HospitalId,
        entity.SiteId,
        entity.DepartmentId,
        entity.IsActive,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.CreatedBy,
        entity.UpdatedBy,
    });

    private static string SerializeRuleSnapshot(WorkflowRuleEntity entity) => JsonSerializer.Serialize(new
    {
        entity.RuleId,
        entity.ProfileId,
        entity.SlotCode,
        entity.Priority,
        entity.IsEnabled,
        entity.ConditionJson,
        entity.ConfigJson,
        entity.EffectiveFrom,
        entity.EffectiveTo,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.CreatedBy,
        entity.UpdatedBy,
    });

    public async Task<IReadOnlyList<WorkflowConfigChangeLogDto>> GetChangeLogAsync(Guid profileId, int limit, CancellationToken ct)
    {
        if (limit <= 0)
        {
            limit = 100;
        }
        if (limit > 1000)
        {
            limit = 1000;
        }

        var rows = await _dbContext.WorkflowConfigChangeLogs
            .AsNoTracking()
            .Where(x => x.ProfileId == profileId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.ChangeLogId)
            .Take(limit)
            .ToListAsync(ct);

        return rows.Select(x => new WorkflowConfigChangeLogDto(
            x.ChangeLogId,
            x.EntityType,
            x.EntityId,
            x.ProfileId,
            x.Action,
            x.ActorId,
            x.CreatedAt,
            x.ChangeReason,
            x.SnapshotJson)).ToList();
    }
}
