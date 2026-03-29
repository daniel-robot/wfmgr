namespace Wfmgr.Infrastructure.Persistence.Entities;

public class PlanVersionEntity
{
    public Guid PlanVersionId { get; set; }
    public Guid CaseId { get; set; }
    public int VersionNo { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? SummaryJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public CaseEntity Case { get; set; } = null!;
}