using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Integrations.Messaging.Sagas;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public sealed class ContouringSagaStateConfiguration : IEntityTypeConfiguration<ContouringSagaState>
{
    public void Configure(EntityTypeBuilder<ContouringSagaState> builder)
    {
        builder.ToTable("ContouringSagaState");

        builder.HasKey(x => x.CorrelationId);
        // MassTransit supplies the correlation id (= case id) — never auto-generate it,
        // otherwise EF will overwrite the saga instance's id on insert and the saga will
        // never correlate subsequent events.
        builder.Property(x => x.CorrelationId).ValueGeneratedNever();

        builder.Property(x => x.CurrentState).HasMaxLength(64).IsRequired();
        builder.Property(x => x.AccessionNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TransitionCode).HasMaxLength(64);
        builder.Property(x => x.TriggeredBy).HasMaxLength(128);
        builder.Property(x => x.FaultReason).HasMaxLength(256);

        // Optimistic concurrency token used by MassTransit's EF saga repository.
        builder.Property(x => x.Version).IsConcurrencyToken();

        builder.HasIndex(x => x.CurrentState);
    }
}
