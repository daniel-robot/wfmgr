using Microsoft.EntityFrameworkCore;
using Npgsql;
using Wfmgr.Infrastructure.Persistence;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Profiles;

/// <summary>
/// Lazily seeds the two default <see cref="WorkflowProfileEntity"/> rows
/// (global + RT department) when the <c>WorkflowProfile</c> table is empty.
/// <para>
/// Mirrors the profile seed in <c>database/init.sql</c> so that creating an
/// empty database and running EF migrations (without the docker-compose init
/// script) still produces the default profiles. Follows the same race-safe
/// pattern used by <c>WorkflowTransitionCatalogService.EnsureSeededAsync</c>.
/// </para>
/// </summary>
internal static class WorkflowProfileSeeder
{
    // Process-wide one-shot guard: once we've successfully verified or seeded
    // the table in this process, subsequent resolver calls skip the AnyAsync.
    private static int _seeded;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static readonly Guid GlobalProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DepartmentProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static async Task EnsureSeededAsync(WfmgrDbContext db, CancellationToken ct)
    {
        if (Volatile.Read(ref _seeded) == 1) return;

        await Gate.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref _seeded) == 1) return;

            if (await db.WorkflowProfiles.AnyAsync(ct))
            {
                Volatile.Write(ref _seeded, 1);
                return;
            }

            var now = DateTimeOffset.UtcNow;

            db.WorkflowProfiles.Add(new WorkflowProfileEntity
            {
                ProfileId = GlobalProfileId,
                HospitalId = null,
                SiteId = null,
                DepartmentId = null,
                Name = "Global Default Workflow",
                Version = 1,
                IsActive = true,
                CreatedAt = now,
            });

            db.WorkflowProfiles.Add(new WorkflowProfileEntity
            {
                ProfileId = DepartmentProfileId,
                HospitalId = "HOSP001",
                SiteId = "SITE_A",
                DepartmentId = "RT",
                Name = "RT Department Workflow",
                Version = 1,
                IsActive = true,
                CreatedAt = now,
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsSeedRace(ex))
            {
                // A concurrent caller seeded first; safe to ignore once data exists.
                db.ChangeTracker.Clear();
            }

            Volatile.Write(ref _seeded, 1);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static bool IsSeedRace(DbUpdateException ex)
        => ex.InnerException is PostgresException pg
           && pg.SqlState == PostgresErrorCodes.UniqueViolation;

    // Test hook — allows tests using ephemeral per-test DBs to re-seed.
    internal static void ResetForTesting() => Volatile.Write(ref _seeded, 0);
}
