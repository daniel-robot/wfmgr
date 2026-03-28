using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class WorkflowRuleEntityConfiguration : IEntityTypeConfiguration<WorkflowRuleEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowRuleEntity> builder)
    {
        builder.ToTable("WorkflowRule");

        builder.HasKey(x => x.RuleId);

        builder.Property(x => x.SlotCode).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Priority).IsRequired();
    builder.Property(x => x.ConditionJson).HasColumnType("text");
    builder.Property(x => x.ConfigJson).HasColumnType("text").IsRequired();
        builder.Property(x => x.IsEnabled).IsRequired();

        builder.HasIndex(x => new { x.ProfileId, x.SlotCode, x.IsEnabled });
        builder.HasIndex(x => new { x.ProfileId, x.SlotCode, x.Priority });

        builder.HasOne(x => x.Profile)
            .WithMany(x => x.Rules)
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
