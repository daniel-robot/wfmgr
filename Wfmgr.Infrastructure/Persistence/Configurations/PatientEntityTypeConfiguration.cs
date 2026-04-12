using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class PatientEntityTypeConfiguration : IEntityTypeConfiguration<PatientEntity>
{
    public void Configure(EntityTypeBuilder<PatientEntity> builder)
    {
        builder.ToTable("Patient");

        builder.HasKey(x => x.PatientId);

        builder.Property(x => x.HospitalId).HasMaxLength(32).IsRequired();
        builder.Property(x => x.SiteId).HasMaxLength(32).IsRequired();
        builder.Property(x => x.DepartmentId).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ExternalPatientId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DateOfBirth).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => new { x.HospitalId, x.SiteId, x.ExternalPatientId }).IsUnique();
    }
}
