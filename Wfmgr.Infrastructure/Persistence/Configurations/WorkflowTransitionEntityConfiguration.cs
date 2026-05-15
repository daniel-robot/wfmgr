using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class WorkflowTransitionEntityConfiguration : IEntityTypeConfiguration<WorkflowTransitionEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowTransitionEntity> builder)
    {
        builder.ToTable("WorkflowTransition");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Phase).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.ToStatus).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TriggerName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.TriggerType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ConfigSlot).HasMaxLength(64);
        builder.Property(x => x.Description).HasMaxLength(2048);
        builder.Property(x => x.IsEnabled).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        // PostgreSQL-only: map xmin system column as a row-version concurrency token.
        // Reserved for Phase 2 (admin edit endpoints). InMemory provider ignores this.
        builder.Property<uint>("Xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.Phase, x.SortOrder });

        builder.HasMany(x => x.FromStatuses)
            .WithOne(x => x.Transition!)
            .HasForeignKey(x => x.TransitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Attributes)
            .WithOne(x => x.Transition!)
            .HasForeignKey(x => x.TransitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkflowTransitionFromStatusEntityConfiguration : IEntityTypeConfiguration<WorkflowTransitionFromStatusEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowTransitionFromStatusEntity> builder)
    {
        builder.ToTable("WorkflowTransitionFromStatus");

        builder.HasKey(x => new { x.TransitionId, x.FromStatus });

        builder.Property(x => x.FromStatus).HasMaxLength(64).IsRequired();
    }
}

public class WorkflowTransitionAttributeEntityConfiguration : IEntityTypeConfiguration<WorkflowTransitionAttributeEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowTransitionAttributeEntity> builder)
    {
        builder.ToTable("WorkflowTransitionAttribute");

        builder.HasKey(x => new { x.TransitionId, x.Kind, x.Value });

        builder.Property(x => x.Kind).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();
    }
}
