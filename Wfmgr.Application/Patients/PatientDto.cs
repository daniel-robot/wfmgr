namespace Wfmgr.Application.Patients;

public record PatientDto(
    Guid PatientId,
    string HospitalId,
    string SiteId,
    string DepartmentId,
    string ExternalPatientId,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public class CreatePatientRequest
{
    public string HospitalId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string ExternalPatientId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
}
