using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<OutboxMessageEntity> builder)
    {
        builder.ToTable("OutboxMessage");

        builder.HasKey(x => x.MessageId);

        builder.Property(x => x.TargetSystem).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(64).IsRequired();
    builder.Property(x => x.PayloadJson).HasColumnType("text").IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.RetryCount).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.MessageType).HasMaxLength(256);
        builder.Property(x => x.SchemaVersion).IsRequired().HasDefaultValue(1);
        builder.Property(x => x.Traceparent).HasMaxLength(128);
        builder.Property(x => x.DeliveryMode).HasConversion<int>().IsRequired().HasDefaultValue(Wfmgr.Domain.Integrations.OutboxDeliveryMode.Http);

        builder.HasIndex(x => new { x.Status, x.NextRetryAt });
        builder.HasIndex(x => x.CorrelationId);

        builder.HasOne(x => x.Case)
            .WithMany(x => x.OutboxMessages)
            .HasForeignKey(x => x.CaseId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
