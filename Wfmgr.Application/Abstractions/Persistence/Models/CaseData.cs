using Wfmgr.Domain.Enums;

namespace Wfmgr.Application.Abstractions.Persistence.Models;

public class CaseData
{
    public Guid CaseId { get; set; }
    public string HospitalId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string? PatientId { get; set; }
    public string AccessionNumber { get; set; } = string.Empty;
    public CaseStatus CurrentStatus { get; set; }
    public int StatusVersion { get; set; }
    public string? CtStudyInstanceUid { get; set; }
    public string? CtWadoRsUrl { get; set; }
    public string? PvMedJobId { get; set; }
    public string? RtStructSeriesInstanceUid { get; set; }
    public string? Notes { get; set; }
    public string? CurrentPlannerUserId { get; set; }
    public string? CurrentReviewerUserId { get; set; }
    public int? CurrentPlanVersionNo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
