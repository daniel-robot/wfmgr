using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class WorkflowVocabularyTermEntityConfiguration : IEntityTypeConfiguration<WorkflowVocabularyTermEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowVocabularyTermEntity> builder)
    {
        builder.ToTable("WorkflowVocabularyTerm");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(256);
        builder.Property(x => x.Description).HasMaxLength(2048);
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.IsSystem).IsRequired();
        builder.Property(x => x.IsEnabled).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        // PostgreSQL-only row-version concurrency token; InMemory ignores this.
        builder.Property<uint>("Xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();

        builder.HasIndex(x => new { x.Kind, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.Kind, x.SortOrder });
    }
}

public class WorkflowVocabularyChangeLogEntityConfiguration : IEntityTypeConfiguration<WorkflowVocabularyChangeLogEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowVocabularyChangeLogEntity> builder)
    {
        builder.ToTable("WorkflowVocabularyChangeLog");

        builder.HasKey(x => x.ChangeLogId);

        builder.Property(x => x.Kind).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ActorId).HasMaxLength(128);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ChangeReason).HasMaxLength(512);
        builder.Property(x => x.SnapshotJson).HasColumnType("jsonb");

        builder.HasIndex(x => new { x.TermId, x.CreatedAt });
        builder.HasIndex(x => new { x.Kind, x.Code, x.CreatedAt });
    }
}
