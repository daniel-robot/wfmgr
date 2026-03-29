using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class CaseAttachmentEntityConfiguration : IEntityTypeConfiguration<CaseAttachmentEntity>
{
    public void Configure(EntityTypeBuilder<CaseAttachmentEntity> builder)
    {
        builder.ToTable("CaseAttachment");

        builder.HasKey(x => x.AttachmentId);

        builder.Property(x => x.Category).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.StoragePath).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.SourceSystem).HasMaxLength(64);
        builder.Property(x => x.UploadedBy).HasMaxLength(128);
        builder.Property(x => x.UploadedAt).IsRequired();

        builder.HasIndex(x => x.CaseId);

        builder.HasOne(x => x.Case)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}