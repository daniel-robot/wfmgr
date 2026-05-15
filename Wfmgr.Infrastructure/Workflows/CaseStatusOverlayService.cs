using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wfmgr.Application.Workflows.V1.CaseStatuses;
using Wfmgr.Domain.Enums;
using Wfmgr.Infrastructure.Persistence;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Workflows;

/// <summary>
/// DB-backed implementation of <see cref="ICaseStatusOverlayService"/>.
/// Lazily seeds one overlay row per <see cref="CaseStatus"/> enum value on
/// first read; mirrors the lazy-seed / xmin-concurrency pattern used by the
/// other Phase 1-3 catalog services. No change log: overlay edits are purely
/// cosmetic and do not affect engine behaviour.
/// </summary>
public sealed class CaseStatusOverlayService : ICaseStatusOverlayService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CaseStatusOverlayService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<CaseStatusOverlayDto>? _cache;

    public CaseStatusOverlayService(
        IServiceProvider serviceProvider,
        ILogger<CaseStatusOverlayService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CaseStatusOverlayDto>> ListAllAsync(CancellationToken ct)
    {
        if (_cache is not null) return _cache;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cache is not null) return _cache;

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
            await EnsureSeededAsync(db, ct);

            var rows = await db.WorkflowCaseStatusOverlays
                .AsNoTracking()
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Code)
                .ToListAsync(ct);

            _cache = rows.Select(r => ToDto(r, GetXmin(db, r))).ToList();
            return _cache;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CaseStatusOverlayDto?> GetByCodeAsync(string code, CancellationToken ct)
    {
        var all = await ListAllAsync(ct);
        return all.FirstOrDefault(x => x.Code == code);
    }

    public async Task<CaseStatusOverlayMutationResult> UpdateAsync(
        string code,
        UpdateCaseStatusOverlayRequest request,
        CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        await EnsureSeededAsync(db, ct);

        var entity = await db.WorkflowCaseStatusOverlays.FirstOrDefaultAsync(x => x.Code == code, ct);
        if (entity is null) return CaseStatusOverlayMutationResult.NotFoundResult();

        var currentHash = ComputeHash(entity, GetXmin(db, entity));
        if (!string.IsNullOrWhiteSpace(request.ExpectedHash) &&
            !string.Equals(request.ExpectedHash, currentHash, StringComparison.Ordinal))
        {
            return CaseStatusOverlayMutationResult.ConflictResult(new CaseStatusOverlayMutationConflictDto(
                "Overlay has been modified since last read.", currentHash));
        }

        var (errors, warnings) = ValidateFields(request);
        if (errors.Count > 0)
        {
            return CaseStatusOverlayMutationResult.Invalid(
                new ValidateCaseStatusOverlayResponse(false, errors, warnings));
        }

        entity.DisplayName = NormalizeOptional(request.DisplayName);
        entity.Description = NormalizeOptional(request.Description);
        entity.Color = NormalizeOptional(request.Color);
        entity.Category = NormalizeOptional(request.Category);
        if (request.SortOrder.HasValue) entity.SortOrder = request.SortOrder.Value;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            var fresh = await db.WorkflowCaseStatusOverlays.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, ct);
            var freshHash = fresh is null ? null : ComputeHash(fresh, GetXmin(db, fresh));
            return CaseStatusOverlayMutationResult.ConflictResult(
                new CaseStatusOverlayMutationConflictDto("Overlay was modified concurrently.", freshHash));
        }

        InvalidateCache();
        return CaseStatusOverlayMutationResult.Success(ToDto(entity, GetXmin(db, entity)));
    }

    public async Task<CaseStatusOverlayMutationResult> ResetAsync(string code, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        await EnsureSeededAsync(db, ct);

        var entity = await db.WorkflowCaseStatusOverlays.FirstOrDefaultAsync(x => x.Code == code, ct);
        if (entity is null) return CaseStatusOverlayMutationResult.NotFoundResult();

        if (!Enum.TryParse<CaseStatus>(code, out var status))
        {
            return CaseStatusOverlayMutationResult.Invalid(new ValidateCaseStatusOverlayResponse(
                false, [$"code '{code}' is not a known CaseStatus enum value."], []));
        }

        var defaults = BuildDefault(status);
        entity.DisplayName = defaults.DisplayName;
        entity.Description = defaults.Description;
        entity.Color = defaults.Color;
        entity.Category = defaults.Category;
        entity.SortOrder = defaults.SortOrder;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            var fresh = await db.WorkflowCaseStatusOverlays.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, ct);
            var freshHash = fresh is null ? null : ComputeHash(fresh, GetXmin(db, fresh));
            return CaseStatusOverlayMutationResult.ConflictResult(
                new CaseStatusOverlayMutationConflictDto("Overlay was modified concurrently.", freshHash));
        }

        InvalidateCache();
        return CaseStatusOverlayMutationResult.Success(ToDto(entity, GetXmin(db, entity)));
    }

    public void InvalidateCache()
    {
        _gate.Wait();
        try { _cache = null; }
        finally { _gate.Release(); }
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private async Task EnsureSeededAsync(WfmgrDbContext db, CancellationToken ct)
    {
        var existingCodes = await db.WorkflowCaseStatusOverlays
            .Select(x => x.Code)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existingCodes, StringComparer.Ordinal);

        var now = DateTimeOffset.UtcNow;
        var added = false;

        foreach (var status in Enum.GetValues<CaseStatus>())
        {
            var code = status.ToString();
            if (existingSet.Contains(code)) continue;

            var defaults = BuildDefault(status);
            db.WorkflowCaseStatusOverlays.Add(new WorkflowCaseStatusOverlayEntity
            {
                Code = code,
                Value = (int)status,
                DisplayName = defaults.DisplayName,
                Description = defaults.Description,
                Color = defaults.Color,
                Category = defaults.Category,
                SortOrder = defaults.SortOrder,
                CreatedAt = now,
            });
            added = true;
        }

        if (!added) return;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Concurrent seed race; safe to ignore once data exists.
            _logger.LogDebug(ex, "Case-status overlay seed race ignored (rows already present).");
            db.ChangeTracker.Clear();
        }
    }

    private static (string? DisplayName, string? Description, string? Color, string? Category, int SortOrder)
        BuildDefault(CaseStatus status)
    {
        var name = status.ToString();
        var category = InferCategory(name);
        var color = InferColor(category, name);
        return (
            DisplayName: SplitCamelCase(name),
            Description: null,
            Color: color,
            Category: category,
            SortOrder: (int)status);
    }

    private static string InferCategory(string name)
    {
        if (name.StartsWith("Sim", StringComparison.Ordinal)) return "Simulation";
        if (name.StartsWith("Image", StringComparison.Ordinal)) return "ImageAcquisition";
        if (name.StartsWith("AutoContour", StringComparison.Ordinal)
            || name.StartsWith("ManualContour", StringComparison.Ordinal)
            || name.StartsWith("Contour", StringComparison.Ordinal)) return "Contouring";
        if (name.StartsWith("Planning", StringComparison.Ordinal)
            || name.StartsWith("Plan", StringComparison.Ordinal) && !name.StartsWith("PlanQA", StringComparison.Ordinal)) return "Planning";
        if (name.StartsWith("PlanQA", StringComparison.Ordinal) || name == "PlanDoubleCheckOptional") return "PlanQA";
        if (name == "Cancelled") return "Terminal";
        return "Other";
    }

    private static string InferColor(string category, string name)
    {
        if (name == "Cancelled") return "#9e9e9e";
        return category switch
        {
            "Simulation" => "#1976d2",
            "ImageAcquisition" => "#0097a7",
            "Contouring" => "#7b1fa2",
            "Planning" => "#f57c00",
            "PlanQA" => "#388e3c",
            _ => "#607d8b",
        };
    }

    private static string SplitCamelCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length + 8);
        sb.Append(s[0]);
        for (var i = 1; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsUpper(c) && !char.IsUpper(s[i - 1])) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static (List<string> Errors, List<string> Warnings) ValidateFields(UpdateCaseStatusOverlayRequest request)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Color)
            && !System.Text.RegularExpressions.Regex.IsMatch(request.Color!, "^#?[0-9a-fA-F]{3,8}$"))
        {
            warnings.Add($"color '{request.Color}' is not a 3/6/8-digit hex value; the UI may not render it consistently.");
        }

        if (request.SortOrder.HasValue && request.SortOrder.Value < 0)
        {
            errors.Add("sortOrder must be \u2265 0.");
        }

        return (errors, warnings);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static CaseStatusOverlayDto ToDto(WorkflowCaseStatusOverlayEntity row, uint xmin) => new(
        row.Code, row.Value, row.DisplayName, row.Description, row.Color, row.Category, row.SortOrder,
        ComputeHash(row, xmin), row.CreatedAt, row.UpdatedAt);

    private static uint GetXmin(WfmgrDbContext db, WorkflowCaseStatusOverlayEntity entity)
    {
        var entry = db.Entry(entity);
        var prop = entry.Metadata.FindProperty("Xmin");
        if (prop is null) return 0u;
        var value = entry.Property("Xmin").CurrentValue;
        return value is uint u ? u : 0u;
    }

    private static string ComputeHash(WorkflowCaseStatusOverlayEntity row, uint xmin)
    {
        if (xmin != 0) return xmin.ToString("x");

        var sb = new StringBuilder();
        sb.Append(row.Code).Append('|').Append(row.Value).Append('|')
            .Append(row.DisplayName).Append('|').Append(row.Description).Append('|')
            .Append(row.Color).Append('|').Append(row.Category).Append('|')
            .Append(row.SortOrder).Append('|')
            .Append(row.UpdatedAt?.ToUnixTimeMilliseconds() ?? row.CreatedAt.ToUnixTimeMilliseconds());

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = System.Security.Cryptography.SHA1.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
