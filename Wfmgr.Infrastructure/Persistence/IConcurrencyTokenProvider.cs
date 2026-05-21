using Microsoft.EntityFrameworkCore;

namespace Wfmgr.Infrastructure.Persistence;

/// <summary>
/// Strategy for obtaining a row-version token from a tracked entity. Returns a non-zero
/// token when the configured database provider supports row-versioning; returns 0 to
/// signal the caller should fall back to a content hash.
/// <para>
/// This abstraction keeps Postgres-specific xmin handling out of the catalog services
/// so the workflow engine can run on InMemory (tests), Postgres (production), or any
/// other provider via a swap-in implementation.
/// </para>
/// </summary>
public interface IConcurrencyTokenProvider
{
    uint GetToken(DbContext db, object entity);
}

/// <summary>
/// Default provider that reads the shadow <c>Xmin</c> property when present (Postgres) and
/// otherwise returns 0. This works transparently for InMemory, where consumers fall back
/// to a content hash.
/// </summary>
public sealed class DefaultConcurrencyTokenProvider : IConcurrencyTokenProvider
{
    public uint GetToken(DbContext db, object entity)
    {
        var entry = db.Entry(entity);
        var prop = entry.Metadata.FindProperty("Xmin");
        if (prop is null) return 0u;
        var value = entry.Property("Xmin").CurrentValue;
        return value is uint u ? u : 0u;
    }
}
