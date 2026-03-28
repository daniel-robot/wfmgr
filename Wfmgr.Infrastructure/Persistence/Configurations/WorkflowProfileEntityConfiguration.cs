using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class WorkflowProfileEntityConfiguration : IEntityTypeConfiguration<WorkflowProfileEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowProfileEntity> builder)
    {
        builder.ToTable("WorkflowProfile");

        builder.HasKey(x => x.ProfileId);

        builder.Property(x => x.HospitalId).HasMaxLength(32);
        builder.Property(x => x.SiteId).HasMaxLength(32);
        builder.Property(x => x.DepartmentId).HasMaxLength(32);
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Version).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.HospitalId, x.SiteId, x.DepartmentId, x.Version }).IsUnique();
    }
}
