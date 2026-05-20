using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Application.Workflows.V1.Gates;
using Wfmgr.Application.Workflows.V1.Vocabulary;
using Wfmgr.Domain.Enums;
using Wfmgr.Infrastructure.Persistence;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Workflows;

/// <summary>
/// DB-backed implementation of <see cref="IWorkflowTransitionCatalogService"/>.
/// <para>
/// Loads transitions from the <c>WorkflowTransition*</c> tables on first use,
/// caches them in-process, and lazily seeds the tables from the static
/// <see cref="WorkflowTransitionCatalog"/> when the database is empty.
/// </para>
/// <para>
/// Lazy seeding keeps tests (which strip <c>IHostedService</c>s) working without
/// any extra setup, and ensures production behaviour is identical regardless of
/// whether the seed ran at startup.
/// </para>
/// </summary>
public sealed class WorkflowTransitionCatalogService : IWorkflowTransitionCatalogService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowTransitionCatalogService> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<TransitionDefinition>? _cache;
    private IReadOnlyDictionary<string, TransitionDefinition>? _byCode;

    public WorkflowTransitionCatalogService(
        IServiceProvider serviceProvider,
        ILogger<WorkflowTransitionCatalogService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TransitionDefinition>> GetAllAsync(CancellationToken ct)
    {
        return await EnsureLoadedAsync(ct);
    }

    public async Task<TransitionDefinition?> FindByCodeAsync(string code, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);
        return _byCode!.TryGetValue(code, out var def) ? def : null;
    }

    public async Task<TransitionDefinition?> FindByTriggerAsync(string triggerName, CaseStatus fromStatus, CancellationToken ct)
    {
        var all = await EnsureLoadedAsync(ct);
        return all.FirstOrDefault(t =>
            t.TriggerName.Equals(triggerName, StringComparison.OrdinalIgnoreCase)
            && t.FromStatuses.Contains(fromStatus));
    }

    public void InvalidateCache()
    {
        _gate.Wait();
        try
        {
            _cache = null;
            _byCode = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    // ── Admin / mutation surface (Phase 2) ───────────────────────────────────

    public async Task<IReadOnlyList<WorkflowTransitionDto>> ListAllAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();

        // Ensure the table is seeded the first time we list (parity with read path).
        await EnsureSeededAsync(db, ct);

        var rows = await db.WorkflowTransitions
            .AsNoTracking()
            .Include(x => x.FromStatuses)
            .Include(x => x.Attributes)
            .OrderBy(x => x.Phase)
            .ThenBy(x => x.SortOrder)
            .ToListAsync(ct);

        return rows.Select(r => ToDto(r, GetXmin(db, r))).ToList();
    }

    public async Task<WorkflowTransitionDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        var row = await LoadOneAsync(db, x => x.Id == id, ct);
        return row is null ? null : ToDto(row, GetXmin(db, row));
    }

    public async Task<WorkflowTransitionDto?> GetByCodeAsync(string code, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        var row = await LoadOneAsync(db, x => x.Code == code, ct);
        return row is null ? null : ToDto(row, GetXmin(db, row));
    }

    public async Task<ValidateWorkflowTransitionResponse> ValidateAsync(
        string code,
        string toStatus,
        string triggerType,
        IReadOnlyList<string> fromStatuses,
        IReadOnlyList<string>? requiredRoles,
        IReadOnlyList<string>? gateChecks,
        IReadOnlyList<string>? successActions,
        IReadOnlyList<string>? failureActions,
        IReadOnlyList<string>? workItemsToCreate,
        string? configSlot,
        CancellationToken ct)
    {
        var (extraRoles, extraWorkItems) = await GetExtraVocabularyAsync(ct);
        var (errors, warnings) = WorkflowTransitionGraphValidator.ValidateOne(
            code, toStatus, triggerType, fromStatuses,
            requiredRoles ?? [], gateChecks ?? [], successActions ?? [],
            failureActions ?? [], workItemsToCreate ?? [], configSlot,
            extraRoles, extraWorkItems);

        return new ValidateWorkflowTransitionResponse(errors.Count == 0, errors, warnings);
    }

    public async Task<WorkflowTransitionMutationResult> CreateAsync(
        CreateWorkflowTransitionRequest request,
        string? actorId,
        CancellationToken ct)
    {
        var (extraRoles, extraWorkItems) = await GetExtraVocabularyAsync(ct);
        var (errors, warnings) = WorkflowTransitionGraphValidator.ValidateOne(
            request.Code, request.ToStatus, request.TriggerType, request.FromStatuses,
            request.RequiredRoles ?? [], request.GateChecks ?? [], request.SuccessActions ?? [],
            request.FailureActions ?? [], request.WorkItemsToCreate ?? [], request.ConfigSlot,
            extraRoles, extraWorkItems);

        if (errors.Count > 0)
        {
            return WorkflowTransitionMutationResult.Invalid(
                new ValidateWorkflowTransitionResponse(false, errors, warnings));
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();

        if (await db.WorkflowTransitions.AnyAsync(x => x.Code == request.Code, ct))
        {
            return WorkflowTransitionMutationResult.Invalid(new ValidateWorkflowTransitionResponse(
                false, [$"transition with code '{request.Code}' already exists."], warnings));
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new WorkflowTransitionEntity
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Phase = string.IsNullOrWhiteSpace(request.Phase) ? "Other" : request.Phase,
            SortOrder = request.SortOrder,
            ToStatus = request.ToStatus,
            TriggerName = request.TriggerName,
            TriggerType = request.TriggerType,
            ConfigSlot = request.ConfigSlot,
            Description = request.Description,
            IsEnabled = true,
            CreatedAt = now,
        };

        ApplyChildren(entity, request.FromStatuses, request.RequiredRoles, request.GateChecks,
            request.SuccessActions, request.FailureActions, request.WorkItemsToCreate);

        db.WorkflowTransitions.Add(entity);
        await db.SaveChangesAsync(ct);

        var dto = ToDto(entity, GetXmin(db, entity));
        WriteChangeLog(db, entity, "Create", actorId, request.ChangeReason, dto);
        await db.SaveChangesAsync(ct);

        InvalidateCache();
        return WorkflowTransitionMutationResult.Success(dto);
    }

    public async Task<WorkflowTransitionMutationResult> UpdateAsync(
        Guid id,
        UpdateWorkflowTransitionRequest request,
        string? actorId,
        CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();

        var entity = await db.WorkflowTransitions
            .Include(x => x.FromStatuses)
            .Include(x => x.Attributes)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return WorkflowTransitionMutationResult.NotFoundResult();

        var currentHash = ComputeHash(entity, GetXmin(db, entity));
        if (!string.IsNullOrWhiteSpace(request.ExpectedHash) &&
            string.Equals(request.ExpectedHash, currentHash, StringComparison.Ordinal))
        {
            return WorkflowTransitionMutationResult.ConflictResult(new WorkflowTransitionMutationConflictDto(
                "Transition has been modified since last read.", currentHash));
        }

        var (extraRoles, extraWorkItems) = await GetExtraVocabularyAsync(ct);
        var (errors, warnings) = WorkflowTransitionGraphValidator.ValidateOne(
            entity.Code, request.ToStatus, request.TriggerType, request.FromStatuses,
            request.RequiredRoles ?? [], request.GateChecks ?? [], request.SuccessActions ?? [],
            request.FailureActions ?? [], request.WorkItemsToCreate ?? [], request.ConfigSlot,
            extraRoles, extraWorkItems);

        if (errors.Count > 0)
        {
            return WorkflowTransitionMutationResult.Invalid(
                new ValidateWorkflowTransitionResponse(false, errors, warnings));
        }

        entity.Phase = string.IsNullOrWhiteSpace(request.Phase) ? "Other" : request.Phase;
        entity.SortOrder = request.SortOrder;
        entity.ToStatus = request.ToStatus;
        entity.TriggerName = request.TriggerName;
        entity.TriggerType = request.TriggerType;
        entity.ConfigSlot = request.ConfigSlot;
        entity.Description = request.Description;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        // Replace children atomically.
        db.WorkflowTransitionFromStatuses.RemoveRange(entity.FromStatuses);
        db.WorkflowTransitionAttributes.RemoveRange(entity.Attributes);
        entity.FromStatuses.Clear();
        entity.Attributes.Clear();
        ApplyChildren(entity, request.FromStatuses, request.RequiredRoles, request.GateChecks,
            request.SuccessActions, request.FailureActions, request.WorkItemsToCreate);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            var fresh = await LoadOneAsync(db, x => x.Id == id, ct);
            var freshHash = fresh is null ? null : ComputeHash(fresh, GetXmin(db, fresh));
            return WorkflowTransitionMutationResult.ConflictResult(
                new WorkflowTransitionMutationConflictDto("Transition was modified concurrently.", freshHash));
        }

        var dto = ToDto(entity, GetXmin(db, entity));
        WriteChangeLog(db, entity, "Update", actorId, request.ChangeReason, dto);
        await db.SaveChangesAsync(ct);

        InvalidateCache();
        return WorkflowTransitionMutationResult.Success(dto);
    }

    public async Task<WorkflowTransitionMutationResult> SetEnabledAsync(
        Guid id,
        bool enabled,
        ToggleWorkflowTransitionRequest request,
        string? actorId,
        CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        var entity = await db.WorkflowTransitions
            .Include(x => x.FromStatuses)
            .Include(x => x.Attributes)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return WorkflowTransitionMutationResult.NotFoundResult();

        var currentHash = ComputeHash(entity, GetXmin(db, entity));
        if (!string.IsNullOrWhiteSpace(request.ExpectedHash) &&
            !string.Equals(request.ExpectedHash, currentHash, StringComparison.Ordinal))
        {
            return WorkflowTransitionMutationResult.ConflictResult(new WorkflowTransitionMutationConflictDto(
                "Transition has been modified since last read.", currentHash));
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
                var fresh = await LoadOneAsync(db, x => x.Id == id, ct);
                var freshHash = fresh is null ? null : ComputeHash(fresh, GetXmin(db, fresh));
                return WorkflowTransitionMutationResult.ConflictResult(
                    new WorkflowTransitionMutationConflictDto("Transition was modified concurrently.", freshHash));
            }
        }

        var dto = ToDto(entity, GetXmin(db, entity));
        WriteChangeLog(db, entity, enabled ? "Enable" : "Disable", actorId, request.ChangeReason, dto);
        await db.SaveChangesAsync(ct);

        InvalidateCache();
        return WorkflowTransitionMutationResult.Success(dto);
    }

    public async Task<WorkflowTransitionMutationResult> DeleteAsync(
        Guid id,
        ToggleWorkflowTransitionRequest request,
        string? actorId,
        CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        var entity = await db.WorkflowTransitions
            .Include(x => x.FromStatuses)
            .Include(x => x.Attributes)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return WorkflowTransitionMutationResult.NotFoundResult();

        var currentHash = ComputeHash(entity, GetXmin(db, entity));
        if (!string.IsNullOrWhiteSpace(request.ExpectedHash) &&
            !string.Equals(request.ExpectedHash, currentHash, StringComparison.Ordinal))
        {
            return WorkflowTransitionMutationResult.ConflictResult(new WorkflowTransitionMutationConflictDto(
                "Transition has been modified since last read.", currentHash));
        }

        var snapshot = ToDto(entity, GetXmin(db, entity));
        WriteChangeLog(db, entity, "Delete", actorId, request.ChangeReason, snapshot);

        db.WorkflowTransitions.Remove(entity);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            var fresh = await LoadOneAsync(db, x => x.Id == id, ct);
            var freshHash = fresh is null ? null : ComputeHash(fresh, GetXmin(db, fresh));
            return WorkflowTransitionMutationResult.ConflictResult(
                new WorkflowTransitionMutationConflictDto("Transition was modified concurrently.", freshHash));
        }

        InvalidateCache();
        return WorkflowTransitionMutationResult.Success(snapshot);
    }

    public async Task<IReadOnlyList<WorkflowTransitionChangeLogDto>> GetChangeLogAsync(
        Guid transitionId,
        int limit,
        CancellationToken ct)
    {
        if (limit <= 0) limit = 100;
        if (limit > 500) limit = 500;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();

        var rows = await db.WorkflowTransitionChangeLogs
            .AsNoTracking()
            .Where(x => x.TransitionId == transitionId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return rows.Select(x => new WorkflowTransitionChangeLogDto(
            x.ChangeLogId, x.TransitionId, x.Code, x.Action, x.ActorId,
            x.CreatedAt, x.ChangeReason, x.SnapshotJson)).ToList();
    }

    // ── Mutation helpers ─────────────────────────────────────────────────────

    private static async Task<WorkflowTransitionEntity?> LoadOneAsync(
        WfmgrDbContext db,
        System.Linq.Expressions.Expression<Func<WorkflowTransitionEntity, bool>> predicate,
        CancellationToken ct)
    {
        return await db.WorkflowTransitions
            .Include(x => x.FromStatuses)
            .Include(x => x.Attributes)
            .FirstOrDefaultAsync(predicate, ct);
    }

    private static void ApplyChildren(
        WorkflowTransitionEntity entity,
        IReadOnlyList<string> fromStatuses,
        IReadOnlyList<string>? requiredRoles,
        IReadOnlyList<string>? gateChecks,
        IReadOnlyList<string>? successActions,
        IReadOnlyList<string>? failureActions,
        IReadOnlyList<string>? workItemsToCreate)
    {
        foreach (var fs in fromStatuses.Distinct(StringComparer.Ordinal))
        {
            entity.FromStatuses.Add(new WorkflowTransitionFromStatusEntity
            {
                TransitionId = entity.Id,
                FromStatus = fs,
            });
        }

        AddAttributes(entity, WorkflowTransitionAttributeKinds.RequiredRole, requiredRoles ?? []);
        AddAttributes(entity, WorkflowTransitionAttributeKinds.GateCheck, gateChecks ?? []);
        AddAttributes(entity, WorkflowTransitionAttributeKinds.SuccessAction, successActions ?? []);
        AddAttributes(entity, WorkflowTransitionAttributeKinds.FailureAction, failureActions ?? []);
        AddAttributes(entity, WorkflowTransitionAttributeKinds.WorkItemToCreate, workItemsToCreate ?? []);
    }

    private static WorkflowTransitionDto ToDto(WorkflowTransitionEntity row, uint xmin)
    {
        string[] AttributesOf(string kind) => row.Attributes
            .Where(a => a.Kind == kind)
            .OrderBy(a => a.SortOrder)
            .Select(a => a.Value)
            .ToArray();

        return new WorkflowTransitionDto(
            row.Id, row.Code, row.Phase, row.SortOrder,
            row.ToStatus, row.TriggerName, row.TriggerType,
            row.ConfigSlot, row.Description, row.IsEnabled,
            row.FromStatuses.Select(f => f.FromStatus).OrderBy(s => s).ToArray(),
            AttributesOf(WorkflowTransitionAttributeKinds.RequiredRole),
            AttributesOf(WorkflowTransitionAttributeKinds.GateCheck),
            AttributesOf(WorkflowTransitionAttributeKinds.SuccessAction),
            AttributesOf(WorkflowTransitionAttributeKinds.FailureAction),
            AttributesOf(WorkflowTransitionAttributeKinds.WorkItemToCreate),
            ComputeHash(row, xmin),
            row.CreatedAt, row.UpdatedAt);
    }

    private static uint GetXmin(WfmgrDbContext db, WorkflowTransitionEntity entity)
    {
        var entry = db.Entry(entity);
        var prop = entry.Metadata.FindProperty("Xmin");
        if (prop is null) return 0u;
        var value = entry.Property("Xmin").CurrentValue;
        return value is uint u ? u : 0u;
    }

    private static string ComputeHash(WorkflowTransitionEntity row, uint xmin)
    {
        // Use xmin as the concurrency token when present (Postgres). On InMemory
        // (xmin == 0) fall back to a content hash so the contract still works.
        if (xmin != 0)
        {
            return xmin.ToString("x");
        }

        var sb = new System.Text.StringBuilder();
        sb.Append(row.Code).Append('|').Append(row.Phase).Append('|').Append(row.SortOrder)
            .Append('|').Append(row.ToStatus).Append('|').Append(row.TriggerName)
            .Append('|').Append(row.TriggerType).Append('|').Append(row.ConfigSlot)
            .Append('|').Append(row.IsEnabled).Append('|')
            .Append(row.UpdatedAt?.ToUnixTimeMilliseconds() ?? row.CreatedAt.ToUnixTimeMilliseconds());

        foreach (var f in row.FromStatuses.OrderBy(x => x.FromStatus, StringComparer.Ordinal))
            sb.Append('|').Append(f.FromStatus);
        foreach (var a in row.Attributes
            .OrderBy(x => x.Kind, StringComparer.Ordinal)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Value, StringComparer.Ordinal))
        {
            sb.Append('|').Append(a.Kind).Append(':').Append(a.Value);
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var hash = System.Security.Cryptography.SHA1.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void WriteChangeLog(
        WfmgrDbContext db,
        WorkflowTransitionEntity entity,
        string action,
        string? actorId,
        string? reason,
        WorkflowTransitionDto snapshot)
    {
        db.WorkflowTransitionChangeLogs.Add(new WorkflowTransitionChangeLogEntity
        {
            TransitionId = entity.Id,
            Code = entity.Code,
            Action = action,
            ActorId = actorId,
            CreatedAt = DateTimeOffset.UtcNow,
            ChangeReason = reason,
            SnapshotJson = System.Text.Json.JsonSerializer.Serialize(snapshot),
        });
    }

    // ── Internals ──────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<TransitionDefinition>> EnsureLoadedAsync(CancellationToken ct)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_cache is not null)
            {
                return _cache;
            }

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();

            var rows = await LoadRowsAsync(db, ct);
            if (rows.Count == 0)
            {
                await EnsureSeededAsync(db, ct);
                rows = await LoadRowsAsync(db, ct);
            }

            var defs = rows.Select(MapRow).Where(d => d is not null).Cast<TransitionDefinition>().ToList();
            _cache = defs;
            _byCode = defs.ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);
            return _cache;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<List<WorkflowTransitionEntity>> LoadRowsAsync(WfmgrDbContext db, CancellationToken ct)
    {
        return await db.WorkflowTransitions
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .Include(x => x.FromStatuses)
            .Include(x => x.Attributes)
            .OrderBy(x => x.Phase)
            .ThenBy(x => x.SortOrder)
            .ToListAsync(ct);
    }

    private async Task EnsureSeededAsync(WfmgrDbContext db, CancellationToken ct)
    {
        if (await db.WorkflowTransitions.AnyAsync(ct))
        {
            return;
        }

        _logger.LogInformation(
            "WorkflowTransition table empty — seeding {Count} transitions from static WorkflowTransitionCatalog.",
            WorkflowTransitionCatalog.All.Count);

        try
        {
            await SeedFromStaticCatalogAsync(db, ct);
        }
        catch (DbUpdateException ex) when (IsTransitionSeedRace(ex))
        {
            // A concurrent caller seeded first; safe to ignore once data exists.
            _logger.LogDebug(ex, "WorkflowTransition seed race ignored (rows already present).");
            db.ChangeTracker.Clear();
        }
    }

    private static bool IsTransitionSeedRace(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pg
            && pg.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(pg.ConstraintName, "IX_WorkflowTransition_Code", StringComparison.Ordinal);
    }

    private TransitionDefinition? MapRow(WorkflowTransitionEntity row)
    {
        if (!Enum.TryParse<CaseStatus>(row.ToStatus, ignoreCase: false, out var toStatus))
        {
            _logger.LogError(
                "WorkflowTransition row {Code} has unknown ToStatus '{ToStatus}' — skipping.",
                row.Code, row.ToStatus);
            return null;
        }

        if (!Enum.TryParse<WorkflowTriggerType>(row.TriggerType, ignoreCase: false, out var triggerType))
        {
            _logger.LogError(
                "WorkflowTransition row {Code} has unknown TriggerType '{TriggerType}' — skipping.",
                row.Code, row.TriggerType);
            return null;
        }

        var fromStatuses = new List<CaseStatus>();
        foreach (var fs in row.FromStatuses)
        {
            if (Enum.TryParse<CaseStatus>(fs.FromStatus, ignoreCase: false, out var parsed))
            {
                fromStatuses.Add(parsed);
            }
            else
            {
                _logger.LogError(
                    "WorkflowTransition row {Code} has unknown FromStatus '{FromStatus}' — skipping value.",
                    row.Code, fs.FromStatus);
            }
        }

        if (fromStatuses.Count == 0)
        {
            _logger.LogError("WorkflowTransition row {Code} has no valid FromStatuses — skipping row.", row.Code);
            return null;
        }

        string[] AttributesOf(string kind) => row.Attributes
            .Where(a => a.Kind == kind)
            .OrderBy(a => a.SortOrder)
            .Select(a => a.Value)
            .ToArray();

        return new TransitionDefinition
        {
            Code = row.Code,
            FromStatuses = fromStatuses.ToArray(),
            ToStatus = toStatus,
            TriggerName = row.TriggerName,
            TriggerType = triggerType,
            RequiredRoles = AttributesOf(WorkflowTransitionAttributeKinds.RequiredRole),
            GateChecks = AttributesOf(WorkflowTransitionAttributeKinds.GateCheck),
            SuccessActions = AttributesOf(WorkflowTransitionAttributeKinds.SuccessAction),
            FailureActions = AttributesOf(WorkflowTransitionAttributeKinds.FailureAction),
            WorkItemsToCreate = AttributesOf(WorkflowTransitionAttributeKinds.WorkItemToCreate),
            ConfigSlot = row.ConfigSlot,
        };
    }

    private static async Task SeedFromStaticCatalogAsync(WfmgrDbContext db, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var all = WorkflowTransitionCatalog.All;
        for (var i = 0; i < all.Count; i++)
        {
            var def = all[i];
            var entity = new WorkflowTransitionEntity
            {
                Id = Guid.NewGuid(),
                Code = def.Code,
                Phase = DerivePhase(def.Code),
                SortOrder = i,
                ToStatus = def.ToStatus.ToString(),
                TriggerName = def.TriggerName,
                TriggerType = def.TriggerType.ToString(),
                ConfigSlot = def.ConfigSlot,
                Description = null,
                IsEnabled = true,
                CreatedAt = now,
            };

            for (var j = 0; j < def.FromStatuses.Length; j++)
            {
                entity.FromStatuses.Add(new WorkflowTransitionFromStatusEntity
                {
                    TransitionId = entity.Id,
                    FromStatus = def.FromStatuses[j].ToString(),
                });
            }

            AddAttributes(entity, WorkflowTransitionAttributeKinds.RequiredRole, def.RequiredRoles);
            AddAttributes(entity, WorkflowTransitionAttributeKinds.GateCheck, def.GateChecks);
            AddAttributes(entity, WorkflowTransitionAttributeKinds.SuccessAction, def.SuccessActions);
            AddAttributes(entity, WorkflowTransitionAttributeKinds.FailureAction, def.FailureActions);
            AddAttributes(entity, WorkflowTransitionAttributeKinds.WorkItemToCreate, def.WorkItemsToCreate);

            db.WorkflowTransitions.Add(entity);
        }

        await db.SaveChangesAsync(ct);
    }

    private static void AddAttributes(WorkflowTransitionEntity entity, string kind, IReadOnlyList<string> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            entity.Attributes.Add(new WorkflowTransitionAttributeEntity
            {
                TransitionId = entity.Id,
                Kind = kind,
                Value = values[i],
                SortOrder = i,
            });
        }
    }

    private static string DerivePhase(string code)
    {
        var dash = code.IndexOf('-');
        var prefix = dash > 0 ? code[..dash] : code;
        return prefix switch
        {
            "SIM" => "IntakeSimulation",
            "IMG" => "ImageAcquisition",
            "CON" => "Contouring",
            "PLN" => "Planning",
            "RX" => "ReReviewPrescription",
            "QA" => "PlanQA",
            _ => "Other",
        };
    }

    private async Task<(IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> WorkItems)> GetExtraVocabularyAsync(CancellationToken ct)
    {
        var vocab = _serviceProvider.GetService<IWorkflowVocabularyCatalogService>();
        if (vocab is null) return (Array.Empty<string>(), Array.Empty<string>());

        var rolesTask = vocab.GetEnabledCodesAsync(WorkflowVocabularyKinds.Role, ct);
        var workItemsTask = vocab.GetEnabledCodesAsync(WorkflowVocabularyKinds.WorkItemType, ct);
        var formsTask = vocab.GetEnabledCodesAsync(WorkflowVocabularyKinds.CaseFormType, ct);
        await Task.WhenAll(rolesTask, workItemsTask, formsTask);

        var workItems = new HashSet<string>(workItemsTask.Result, StringComparer.Ordinal);
        foreach (var f in formsTask.Result) workItems.Add(f);
        return (rolesTask.Result, workItems);
    }

}
