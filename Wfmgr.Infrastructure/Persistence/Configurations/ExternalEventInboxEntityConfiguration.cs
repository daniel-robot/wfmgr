using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class ExternalEventInboxEntityConfiguration : IEntityTypeConfiguration<ExternalEventInboxEntity>
{
    public void Configure(EntityTypeBuilder<ExternalEventInboxEntity> builder)
    {
        builder.ToTable("ExternalEventInbox");

        builder.HasKey(x => new { x.Integration, x.ExternalEventId });

        builder.Property(x => x.Integration).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExternalEventId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.MessageType).HasMaxLength(256);
        builder.Property(x => x.PayloadHash).HasMaxLength(128);
        builder.Property(x => x.Traceparent).HasMaxLength(128);
        builder.Property(x => x.ReceivedAt).IsRequired();

        builder.HasIndex(x => x.CaseId);
    }
}
