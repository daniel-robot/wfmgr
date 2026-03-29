namespace Wfmgr.Infrastructure.Persistence.Entities;

public class CaseFormEntity
{
    public Guid FormId { get; set; }
    public Guid CaseId { get; set; }
    public string FormType { get; set; } = string.Empty;
    public int FormVersion { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string? SubmittedBy { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public CaseEntity Case { get; set; } = null!;
    public ICollection<WorkItemEntity> WorkItems { get; set; } = new List<WorkItemEntity>();
}