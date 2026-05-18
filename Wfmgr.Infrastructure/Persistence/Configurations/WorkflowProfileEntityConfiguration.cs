using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class WorkflowProfileEntityConfiguration : IEntityTypeConfiguration<WorkflowProfileEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowProfileEntity> builder)
    {
        builder.ToTable("WorkflowProfile");

        builder.HasKey(x => x.ProfileId);

        builder.Property(x => x.HospitalId).HasMaxLength(32);
        builder.Property(x => x.SiteId).HasMaxLength(32);
        builder.Property(x => x.DepartmentId).HasMaxLength(32);
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Version).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.CreatedBy).HasMaxLength(128);
        builder.Property(x => x.UpdatedBy).HasMaxLength(128);

        // PostgreSQL-only: map xmin system column as a row-version concurrency token.
        // The InMemory provider used by tests ignores this configuration safely.
        builder.Property<uint>("Xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();

        builder.HasIndex(x => new { x.HospitalId, x.SiteId, x.DepartmentId, x.Version }).IsUnique();
    }
}
