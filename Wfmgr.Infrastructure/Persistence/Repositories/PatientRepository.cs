using Microsoft.EntityFrameworkCore;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence.Repositories;

public class PatientRepository : IPatientRepository
{
    private readonly WfmgrDbContext _dbContext;

    public PatientRepository(WfmgrDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PatientData>> GetPatientsAsync(CancellationToken ct)
    {
        var entities = await _dbContext.Patients
            .AsNoTracking()
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ToListAsync(ct);

        return entities.Select(Map).ToList();
    }

    public async Task<PatientData?> GetPatientByIdAsync(Guid patientId, CancellationToken ct)
    {
        var entity = await _dbContext.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PatientId == patientId, ct);

        return entity is null ? null : Map(entity);
    }

    public async Task AddPatientAsync(PatientData patient, CancellationToken ct)
    {
        var entity = new PatientEntity
        {
            PatientId = patient.PatientId,
            HospitalId = patient.HospitalId,
            SiteId = patient.SiteId,
            DepartmentId = patient.DepartmentId,
            ExternalPatientId = patient.ExternalPatientId,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            CreatedAt = patient.CreatedAt,
            UpdatedAt = patient.UpdatedAt
        };

        await _dbContext.Patients.AddAsync(entity, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => _dbContext.SaveChangesAsync(ct);

    private static PatientData Map(PatientEntity e) => new()
    {
        PatientId = e.PatientId,
        HospitalId = e.HospitalId,
        SiteId = e.SiteId,
        DepartmentId = e.DepartmentId,
        ExternalPatientId = e.ExternalPatientId,
        FirstName = e.FirstName,
        LastName = e.LastName,
        DateOfBirth = e.DateOfBirth,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };
}
