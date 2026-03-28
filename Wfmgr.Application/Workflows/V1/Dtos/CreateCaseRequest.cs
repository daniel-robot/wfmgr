namespace Wfmgr.Application.Workflows.V1.Dtos;

public class CreateCaseRequest
{
    public string HospitalId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string AccessionNumber { get; set; } = string.Empty;
    public string? PatientId { get; set; }
    public string? Notes { get; set; }
}
