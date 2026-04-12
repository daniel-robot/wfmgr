using Wfmgr.Application.Abstractions.Persistence.Models;

namespace Wfmgr.Application.Abstractions.Persistence;

public interface IPatientRepository
{
    Task<IReadOnlyList<PatientData>> GetPatientsAsync(CancellationToken ct);
    Task<PatientData?> GetPatientByIdAsync(Guid patientId, CancellationToken ct);
    Task AddPatientAsync(PatientData patient, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
