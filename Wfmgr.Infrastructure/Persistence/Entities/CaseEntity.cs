using Wfmgr.Domain.Enums;

namespace Wfmgr.Infrastructure.Persistence.Entities;

public class CaseEntity
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

    public ICollection<WorkItemEntity> WorkItems { get; set; } = new List<WorkItemEntity>();
    public ICollection<ExternalEventEntity> ExternalEvents { get; set; } = new List<ExternalEventEntity>();
    public ICollection<OutboxMessageEntity> OutboxMessages { get; set; } = new List<OutboxMessageEntity>();
    public ICollection<AuditLogEntity> AuditLogs { get; set; } = new List<AuditLogEntity>();
    public ICollection<CaseTransitionHistoryEntity> TransitionHistories { get; set; } = new List<CaseTransitionHistoryEntity>();
    public ICollection<CaseFormEntity> Forms { get; set; } = new List<CaseFormEntity>();
    public ICollection<CaseAttachmentEntity> Attachments { get; set; } = new List<CaseAttachmentEntity>();
    public ICollection<IntegrationReferenceEntity> IntegrationReferences { get; set; } = new List<IntegrationReferenceEntity>();
    public ICollection<PlanVersionEntity> PlanVersions { get; set; } = new List<PlanVersionEntity>();
}
