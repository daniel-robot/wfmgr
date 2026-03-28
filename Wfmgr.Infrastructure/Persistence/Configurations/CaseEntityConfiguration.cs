using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Configurations;

public class CaseEntityConfiguration : IEntityTypeConfiguration<CaseEntity>
{
    public void Configure(EntityTypeBuilder<CaseEntity> builder)
    {
        builder.ToTable("Case");

        builder.HasKey(x => x.CaseId);

        builder.Property(x => x.HospitalId).HasMaxLength(32).IsRequired();
        builder.Property(x => x.SiteId).HasMaxLength(32).IsRequired();
        builder.Property(x => x.DepartmentId).HasMaxLength(32).IsRequired();
        builder.Property(x => x.PatientId).HasMaxLength(64);
        builder.Property(x => x.AccessionNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CurrentStatus).HasConversion<int>().IsRequired();
        builder.Property(x => x.StatusVersion).IsConcurrencyToken().IsRequired();
        builder.Property(x => x.CtStudyInstanceUid).HasMaxLength(128);
        builder.Property(x => x.CtWadoRsUrl).HasMaxLength(512);
        builder.Property(x => x.PvMedJobId).HasMaxLength(128);
        builder.Property(x => x.RtStructSeriesInstanceUid).HasMaxLength(128);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => new { x.HospitalId, x.SiteId, x.DepartmentId, x.AccessionNumber }).IsUnique();
        builder.HasIndex(x => x.CurrentStatus);
    }
}
