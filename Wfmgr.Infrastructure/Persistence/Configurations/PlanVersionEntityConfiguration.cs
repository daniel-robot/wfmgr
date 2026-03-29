using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class PlanVersionEntityConfiguration : IEntityTypeConfiguration<PlanVersionEntity>
{
    public void Configure(EntityTypeBuilder<PlanVersionEntity> builder)
    {
        builder.ToTable("PlanVersion");

        builder.HasKey(x => x.PlanVersionId);

        builder.Property(x => x.VersionNo).IsRequired();
        builder.Property(x => x.SourceSystem).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.SummaryJson).HasColumnType("text");
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.CaseId, x.VersionNo });

        builder.HasOne(x => x.Case)
            .WithMany(x => x.PlanVersions)
            .HasForeignKey(x => x.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}