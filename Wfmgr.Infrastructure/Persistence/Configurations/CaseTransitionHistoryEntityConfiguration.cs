using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class CaseTransitionHistoryEntityConfiguration : IEntityTypeConfiguration<CaseTransitionHistoryEntity>
{
    public void Configure(EntityTypeBuilder<CaseTransitionHistoryEntity> builder)
    {
        builder.ToTable("CaseTransitionHistory");

        builder.HasKey(x => x.TransitionId);

        builder.Property(x => x.FromStatus).HasMaxLength(64);
        builder.Property(x => x.ToStatus).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TriggerType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TriggerName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.TriggeredBy).HasMaxLength(128);
        builder.Property(x => x.Reason).HasMaxLength(1024);
        builder.Property(x => x.MetadataJson).HasColumnType("text");
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.CaseId, x.CreatedAt });

        builder.HasOne(x => x.Case)
            .WithMany(x => x.TransitionHistories)
            .HasForeignKey(x => x.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}