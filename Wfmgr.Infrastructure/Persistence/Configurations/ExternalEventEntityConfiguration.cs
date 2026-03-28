using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class ExternalEventEntityConfiguration : IEntityTypeConfiguration<ExternalEventEntity>
{
    public void Configure(EntityTypeBuilder<ExternalEventEntity> builder)
    {
        builder.ToTable("ExternalEvent");

        builder.HasKey(x => x.EventId);

        builder.Property(x => x.Source).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Type).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExternalId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.CaseCorrelationKey).HasMaxLength(128);
        builder.Property(x => x.PayloadJson).HasColumnType("text").IsRequired();
        builder.Property(x => x.ProcessStatus).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Error).HasMaxLength(2048);
        builder.Property(x => x.ReceivedAt).IsRequired();

        builder.HasIndex(x => new { x.Source, x.Type, x.ExternalId }).IsUnique();
        builder.HasIndex(x => x.CaseId);

        builder.HasOne(x => x.Case)
            .WithMany(x => x.ExternalEvents)
            .HasForeignKey(x => x.CaseId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
