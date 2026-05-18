using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class WorkflowTransitionChangeLogEntityConfiguration : IEntityTypeConfiguration<WorkflowTransitionChangeLogEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowTransitionChangeLogEntity> builder)
    {
        builder.ToTable("WorkflowTransitionChangeLog");

        builder.HasKey(x => x.ChangeLogId);
        builder.Property(x => x.ChangeLogId).ValueGeneratedOnAdd();

        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ActorId).HasMaxLength(128);
        builder.Property(x => x.ChangeReason).HasMaxLength(1024);
        builder.Property(x => x.SnapshotJson).HasColumnType("text");
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.TransitionId, x.CreatedAt });
        builder.HasIndex(x => new { x.Code, x.CreatedAt });
    }
}
