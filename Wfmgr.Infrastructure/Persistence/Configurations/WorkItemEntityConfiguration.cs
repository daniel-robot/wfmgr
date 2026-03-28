using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class WorkItemEntityConfiguration : IEntityTypeConfiguration<WorkItemEntity>
{
    public void Configure(EntityTypeBuilder<WorkItemEntity> builder)
    {
        builder.ToTable("WorkItem");

        builder.HasKey(x => x.WorkItemId);

        builder.Property(x => x.Type).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.AssignedRole).HasMaxLength(64).IsRequired();
        builder.Property(x => x.AssignedUserId).HasMaxLength(128);
        builder.Property(x => x.ExternalCorrelationId).HasMaxLength(128);
        builder.Property(x => x.PayloadJson).HasColumnType("text");
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.CaseId);
        builder.HasIndex(x => new { x.AssignedRole, x.Status });
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.Case)
            .WithMany(x => x.WorkItems)
            .HasForeignKey(x => x.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
