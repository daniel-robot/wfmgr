using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wfmgr.Application.Workflows.V1.Vocabulary;
using Wfmgr.Domain;
using Wfmgr.Domain.Forms;
using Wfmgr.Domain.WorkItems;
using Wfmgr.Infrastructure.Persistence;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Workflows;

/// <summary>
/// DB-backed implementation of <see cref="IWorkflowVocabularyCatalogService"/>.
/// Mirrors the lazy-seed / xmin-concurrency / change-log pattern used by
/// <see cref="WorkflowTransitionCatalogService"/>.
/// </summary>
public sealed class WorkflowVocabularyCatalogService : IWorkflowVocabularyCatalogService
{
    private static readonly Regex CodeRegex = new("^[A-Za-z][A-Za-z0-9_-]{0,127}$", RegexOptions.Compiled);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowVocabularyCatalogService> _logger;
    private readonly IConcurrencyTokenProvider _concurrencyTokens;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, HashSet<string>>? _enabledByKind;

    public WorkflowVocabularyCatalogService(
        IServiceProvider serviceProvider,
        ILogger<WorkflowVocabularyCatalogService> logger,
        IConcurrencyTokenProvider concurrencyTokens)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _concurrencyTokens = concurrencyTokens;
    }

    public async Task<IReadOnlyList<WorkflowVocabularyTermDto>> ListAllAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        await EnsureSeededAsync(db, ct);

        var rows = await db.WorkflowVocabularyTerms
            .AsNoTracking()
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .ToListAsync(ct);

        return rows.Select(r => ToDto(r, GetXmin(db, r))).ToList();
    }

    public async Task<IReadOnlyList<WorkflowVocabularyTermDto>> ListByKindAsync(string kind, CancellationToken ct)
    {
        if (!WorkflowVocabularyKinds.IsValid(kind)) return Array.Empty<WorkflowVocabularyTermDto>();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        await EnsureSeededAsync(db, ct);

        var rows = await db.WorkflowVocabularyTerms
            .AsNoTracking()
            .Where(x => x.Kind == kind)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .ToListAsync(ct);

        return rows.Select(r => ToDto(r, GetXmin(db, r))).ToList();
    }

    public async Task<WorkflowVocabularyTermDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        await EnsureSeededAsync(db, ct);
        var row = await db.WorkflowVocabularyTerms.FirstOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : ToDto(row, GetXmin(db, row));
    }

    public async Task<WorkflowVocabularyTermDto?> GetByCodeAsync(string kind, string code, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        await EnsureSeededAsync(db, ct);
        var row = await db.WorkflowVocabularyTerms.FirstOrDefaultAsync(x => x.Kind == kind && x.Code == code, ct);
        return row is null ? null : ToDto(row, GetXmin(db, row));
    }

    public async Task<IReadOnlyCollection<string>> GetEnabledCodesAsync(string kind, CancellationToken ct)
    {
        if (!WorkflowVocabularyKinds.IsValid(kind)) return Array.Empty<string>();

        var map = await EnsureCacheAsync(ct);
        return map.TryGetValue(kind, out var set)
            ? (IReadOnlyCollection<string>)set
            : Array.Empty<string>();
    }

    public Task<ValidateWorkflowVocabularyTermResponse> ValidateAsync(string kind, string code, CancellationToken ct)
    {
        var (errors, warnings) = ValidateFields(kind, code);
        return Task.FromResult(new ValidateWorkflowVocabularyTermResponse(errors.Count == 0, errors, warnings));
    }

    public async Task<WorkflowVocabularyMutationResult> CreateAsync(
        CreateWorkflowVocabularyTermRequest request,
        string? actorId,
        CancellationToken ct)
    {
        var (errors, warnings) = ValidateFields(request.Kind, request.Code);
        if (errors.Count > 0)
        {
            return WorkflowVocabularyMutationResult.Invalid(
                new ValidateWorkflowVocabularyTermResponse(false, errors, warnings));
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        await EnsureSeededAsync(db, ct);

        if (await db.WorkflowVocabularyTerms.AnyAsync(x => x.Kind == request.Kind && x.Code == request.Code, ct))
        {
            return WorkflowVocabularyMutationResult.Invalid(new ValidateWorkflowVocabularyTermResponse(
                false, [$"vocabulary term '{request.Code}' already exists for kind '{request.Kind}'."], warnings));
        }

        var sortOrder = request.SortOrder
            ?? (await db.WorkflowVocabularyTerms.Where(x => x.Kind == request.Kind).MaxAsync(x => (int?)x.SortOrder, ct) ?? 0) + 10;

        var now = DateTimeOffset.UtcNow;
        var entity = new WorkflowVocabularyTermEntity
        {
            Id = Guid.NewGuid(),
            Kind = request.Kind,
            Code = request.Code,
            DisplayName = NormalizeOptional(request.DisplayName),
            Description = NormalizeOptional(request.Description),
            SortOrder = sortOrder,
            IsSystem = false,
            IsEnabled = true,
            CreatedAt = now,
        };

        db.WorkflowVocabularyTerms.Add(entity);
        await db.SaveChangesAsync(ct);

        var dto = ToDto(entity, GetXmin(db, entity));
        WriteChangeLog(db, entity, "Create", actorId, request.ChangeReason, dto);
        await db.SaveChangesAsync(ct);

        InvalidateCache();
        return WorkflowVocabularyMutationResult.Success(dto);
    }

    public async Task<WorkflowVocabularyMutationResult> UpdateAsync(
        Guid id,
        UpdateWorkflowVocabularyTermRequest request,
        string? actorId,
        CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();

        var entity = await db.WorkflowVocabularyTerms.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return WorkflowVocabularyMutationResult.NotFoundResult();

        var currentHash = ComputeHash(entity, GetXmin(db, entity));
        if (!string.IsNullOrWhiteSpace(request.ExpectedHash) &&
            !string.Equals(request.ExpectedHash, currentHash, StringComparison.Ordinal))
        {
            return WorkflowVocabularyMutationResult.ConflictResult(new WorkflowVocabularyMutationConflictDto(
                "Term has been modified since last read.", currentHash));
        }

        entity.DisplayName = NormalizeOptional(request.DisplayName);
        entity.Description = NormalizeOptional(request.Description);
        if (request.SortOrder.HasValue) entity.SortOrder = request.SortOrder.Value;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            var fresh = await db.WorkflowVocabularyTerms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            var freshHash = fresh is null ? null : ComputeHash(fresh, GetXmin(db, fresh));
            return WorkflowVocabularyMutationResult.ConflictResult(
                new WorkflowVocabularyMutationConflictDto("Term was modified concurrently.", freshHash));
        }

        var dto = ToDto(entity, GetXmin(db, entity));
        WriteChangeLog(db, entity, "Update", actorId, request.ChangeReason, dto);
        await db.SaveChangesAsync(ct);

        InvalidateCache();
        return WorkflowVocabularyMutationResult.Success(dto);
    }

    public async Task<WorkflowVocabularyMutationResult> SetEnabledAsync(
        Guid id,
        bool enabled,
        ToggleWorkflowVocabularyTermRequest request,
        string? actorId,
        CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();

        var entity = await db.WorkflowVocabularyTerms.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return WorkflowVocabularyMutationResult.NotFoundResult();

        var currentHash = ComputeHash(entity, GetXmin(db, entity));
        if (!string.IsNullOrWhiteSpace(request.ExpectedHash) &&
            !string.Equals(request.ExpectedHash, currentHash, StringComparison.Ordinal))
        {
            return WorkflowVocabularyMutationResult.ConflictResult(new WorkflowVocabularyMutationConflictDto(
                "Term has been modified since last read.", currentHash));
        }

        if (entity.IsEnabled != enabled)
        {
            entity.IsEnabled = enabled;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                var fresh = await db.WorkflowVocabularyTerms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
                var freshHash = fresh is null ? null : ComputeHash(fresh, GetXmin(db, fresh));
                return WorkflowVocabularyMutationResult.ConflictResult(
                    new WorkflowVocabularyMutationConflictDto("Term was modified concurrently.", freshHash));
            }
        }

        var dto = ToDto(entity, GetXmin(db, entity));
        WriteChangeLog(db, entity, enabled ? "Enable" : "Disable", actorId, request.ChangeReason, dto);
        await db.SaveChangesAsync(ct);

        InvalidateCache();
        return WorkflowVocabularyMutationResult.Success(dto);
    }

    public async Task<WorkflowVocabularyMutationResult> DeleteAsync(
        Guid id,
        ToggleWorkflowVocabularyTermRequest request,
        string? actorId,
        CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();

        var entity = await db.WorkflowVocabularyTerms.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return WorkflowVocabularyMutationResult.NotFoundResult();

        if (entity.IsSystem)
        {
            return WorkflowVocabularyMutationResult.Invalid(new ValidateWorkflowVocabularyTermResponse(
                false,
                [$"term '{entity.Code}' is a system-seeded {entity.Kind} and cannot be deleted; disable it instead."],
                Array.Empty<string>()));
        }

        var currentHash = ComputeHash(entity, GetXmin(db, entity));
        if (!string.IsNullOrWhiteSpace(request.ExpectedHash) &&
            !string.Equals(request.ExpectedHash, currentHash, StringComparison.Ordinal))
        {
            return WorkflowVocabularyMutationResult.ConflictResult(new WorkflowVocabularyMutationConflictDto(
                "Term has been modified since last read.", currentHash));
        }

        var snapshot = ToDto(entity, GetXmin(db, entity));
        WriteChangeLog(db, entity, "Delete", actorId, request.ChangeReason, snapshot);

        db.WorkflowVocabularyTerms.Remove(entity);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            var fresh = await db.WorkflowVocabularyTerms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            var freshHash = fresh is null ? null : ComputeHash(fresh, GetXmin(db, fresh));
            return WorkflowVocabularyMutationResult.ConflictResult(
                new WorkflowVocabularyMutationConflictDto("Term was modified concurrently.", freshHash));
        }

        InvalidateCache();
        return WorkflowVocabularyMutationResult.Success(snapshot);
    }

    public async Task<IReadOnlyList<WorkflowVocabularyChangeLogDto>> GetChangeLogAsync(
        Guid termId,
        int limit,
        CancellationToken ct)
    {
        if (limit <= 0) limit = 100;
        if (limit > 500) limit = 500;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();

        var rows = await db.WorkflowVocabularyChangeLogs
            .AsNoTracking()
            .Where(x => x.TermId == termId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return rows.Select(x => new WorkflowVocabularyChangeLogDto(
            x.ChangeLogId, x.TermId, x.Kind, x.Code, x.Action, x.ActorId,
            x.CreatedAt, x.ChangeReason, x.SnapshotJson)).ToList();
    }

    public void InvalidateCache()
    {
        _gate.Wait();
        try { _enabledByKind = null; }
        finally { _gate.Release(); }
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private async Task<Dictionary<string, HashSet<string>>> EnsureCacheAsync(CancellationToken ct)
    {
        if (_enabledByKind is not null) return _enabledByKind;

        await _gate.WaitAsync(ct);
        try
        {
            if (_enabledByKind is not null) return _enabledByKind;

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
            await EnsureSeededAsync(db, ct);

            var rows = await db.WorkflowVocabularyTerms
                .AsNoTracking()
                .Where(x => x.IsEnabled)
                .Select(x => new { x.Kind, x.Code })
                .ToListAsync(ct);

            var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var k in WorkflowVocabularyKinds.All)
            {
                map[k] = new HashSet<string>(StringComparer.Ordinal);
            }
            foreach (var r in rows)
            {
                if (!map.TryGetValue(r.Kind, out var set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    map[r.Kind] = set;
                }
                set.Add(r.Code);
            }
            _enabledByKind = map;
            return _enabledByKind;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureSeededAsync(WfmgrDbContext db, CancellationToken ct)
    {
        if (await db.WorkflowVocabularyTerms.AnyAsync(ct)) return;

        var now = DateTimeOffset.UtcNow;
        var sortOrder = 0;

        void SeedKind(string kind, IEnumerable<string> codes)
        {
            foreach (var code in codes)
            {
                db.WorkflowVocabularyTerms.Add(new WorkflowVocabularyTermEntity
                {
                    Id = Guid.NewGuid(),
                    Kind = kind,
                    Code = code,
                    DisplayName = null,
                    Description = null,
                    SortOrder = sortOrder += 10,
                    IsSystem = true,
                    IsEnabled = true,
                    CreatedAt = now,
                });
            }
            sortOrder = 0;
        }

        SeedKind(WorkflowVocabularyKinds.Role, ConstantsOf(typeof(WorkflowRoles)));
        SeedKind(WorkflowVocabularyKinds.WorkItemType, ConstantsOf(typeof(WorkItemTypes)));
        SeedKind(WorkflowVocabularyKinds.CaseFormType, ConstantsOf(typeof(CaseFormTypes)));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // A concurrent caller seeded first; safe to ignore once data exists.
            _logger.LogDebug(ex, "Vocabulary seed race ignored (data already present).");
            db.ChangeTracker.Clear();
        }
    }

    private static (List<string> Errors, List<string> Warnings) ValidateFields(string kind, string code)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!WorkflowVocabularyKinds.IsValid(kind))
        {
            errors.Add($"kind '{kind}' is not one of: {string.Join(", ", WorkflowVocabularyKinds.All)}.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors.Add("code is required.");
        }
        else if (!CodeRegex.IsMatch(code))
        {
            errors.Add("code must start with a letter and contain only letters, digits, '_' or '-' (max 128 chars).");
        }

        return (errors, warnings);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static WorkflowVocabularyTermDto ToDto(WorkflowVocabularyTermEntity row, uint xmin) =>
        new(row.Id, row.Kind, row.Code, row.DisplayName, row.Description, row.SortOrder,
            row.IsSystem, row.IsEnabled, ComputeHash(row, xmin), row.CreatedAt, row.UpdatedAt);

    private uint GetXmin(WfmgrDbContext db, WorkflowVocabularyTermEntity entity)
        => _concurrencyTokens.GetToken(db, entity);

    private static string ComputeHash(WorkflowVocabularyTermEntity row, uint xmin)
    {
        if (xmin != 0) return xmin.ToString("x");

        var sb = new StringBuilder();
        sb.Append(row.Kind).Append('|').Append(row.Code).Append('|')
            .Append(row.DisplayName).Append('|').Append(row.Description).Append('|')
            .Append(row.SortOrder).Append('|').Append(row.IsSystem).Append('|')
            .Append(row.IsEnabled).Append('|')
            .Append(row.UpdatedAt?.ToUnixTimeMilliseconds() ?? row.CreatedAt.ToUnixTimeMilliseconds());

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = System.Security.Cryptography.SHA1.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void WriteChangeLog(
        WfmgrDbContext db,
        WorkflowVocabularyTermEntity entity,
        string action,
        string? actorId,
        string? reason,
        WorkflowVocabularyTermDto snapshot)
    {
        db.WorkflowVocabularyChangeLogs.Add(new WorkflowVocabularyChangeLogEntity
        {
            TermId = entity.Id,
            Kind = entity.Kind,
            Code = entity.Code,
            Action = action,
            ActorId = actorId,
            CreatedAt = DateTimeOffset.UtcNow,
            ChangeReason = reason,
            SnapshotJson = JsonSerializer.Serialize(snapshot),
        });
    }

    private static IEnumerable<string> ConstantsOf(Type t) => t
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .Where(v => !string.IsNullOrEmpty(v));
}
