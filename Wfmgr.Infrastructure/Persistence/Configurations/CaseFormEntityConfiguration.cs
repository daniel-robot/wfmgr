using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class CaseFormEntityConfiguration : IEntityTypeConfiguration<CaseFormEntity>
{
    public void Configure(EntityTypeBuilder<CaseFormEntity> builder)
    {
        builder.ToTable("CaseForm");

        builder.HasKey(x => x.FormId);

        builder.Property(x => x.FormType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FormVersion).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("text").IsRequired();
        builder.Property(x => x.SubmittedBy).HasMaxLength(128);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => new { x.CaseId, x.FormType });

        builder.HasOne(x => x.Case)
            .WithMany(x => x.Forms)
            .HasForeignKey(x => x.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}