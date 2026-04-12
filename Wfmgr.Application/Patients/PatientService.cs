using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;

namespace Wfmgr.Application.Patients;

public class PatientService : IPatientService
{
    private readonly IPatientRepository _repository;

    public PatientService(IPatientRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<PatientDto>> GetPatientsAsync(CancellationToken ct)
    {
        var items = await _repository.GetPatientsAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<PatientDto?> GetPatientByIdAsync(Guid patientId, CancellationToken ct)
    {
        var data = await _repository.GetPatientByIdAsync(patientId, ct);
        return data is null ? null : ToDto(data);
    }

    public async Task<PatientDto> CreatePatientAsync(CreatePatientRequest request, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var data = new PatientData
        {
            PatientId = Guid.NewGuid(),
            HospitalId = request.HospitalId,
            SiteId = request.SiteId,
            DepartmentId = request.DepartmentId,
            ExternalPatientId = request.ExternalPatientId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.AddPatientAsync(data, ct);
        await _repository.SaveChangesAsync(ct);

        return ToDto(data);
    }

    private static PatientDto ToDto(PatientData d) => new(
        d.PatientId,
        d.HospitalId,
        d.SiteId,
        d.DepartmentId,
        d.ExternalPatientId,
        d.FirstName,
        d.LastName,
        d.DateOfBirth,
        d.CreatedAt,
        d.UpdatedAt);
}
