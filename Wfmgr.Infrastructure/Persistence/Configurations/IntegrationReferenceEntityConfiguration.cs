using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class IntegrationReferenceEntityConfiguration : IEntityTypeConfiguration<IntegrationReferenceEntity>
{
    public void Configure(EntityTypeBuilder<IntegrationReferenceEntity> builder)
    {
        builder.ToTable("IntegrationReference");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SystemName).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExternalEntityType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExternalId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ExternalStatus).HasMaxLength(64);
        builder.Property(x => x.MetadataJson).HasColumnType("text");
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => new { x.CaseId, x.SystemName });

        builder.HasOne(x => x.Case)
            .WithMany(x => x.IntegrationReferences)
            .HasForeignKey(x => x.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}