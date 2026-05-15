using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class WorkflowCaseStatusOverlayEntityConfiguration : IEntityTypeConfiguration<WorkflowCaseStatusOverlayEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowCaseStatusOverlayEntity> builder)
    {
        builder.ToTable("WorkflowCaseStatusOverlay");

        builder.HasKey(x => x.Code);

        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Value).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(256);
        builder.Property(x => x.Description).HasMaxLength(2048);
        builder.Property(x => x.Color).HasMaxLength(32);
        builder.Property(x => x.Category).HasMaxLength(64);
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        // PostgreSQL row-version concurrency token; InMemory ignores this.
        builder.Property<uint>("Xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();

        builder.HasIndex(x => x.SortOrder);
        builder.HasIndex(x => x.Category);
    }
}
