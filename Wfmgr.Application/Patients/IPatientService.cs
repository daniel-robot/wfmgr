namespace Wfmgr.Application.Patients;

public interface IPatientService
{
    Task<IReadOnlyList<PatientDto>> GetPatientsAsync(CancellationToken ct);
    Task<PatientDto?> GetPatientByIdAsync(Guid patientId, CancellationToken ct);
    Task<PatientDto> CreatePatientAsync(CreatePatientRequest request, CancellationToken ct);
}
