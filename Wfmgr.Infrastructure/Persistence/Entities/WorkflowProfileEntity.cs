namespace Wfmgr.Infrastructure.Persistence.Entities;

public class WorkflowProfileEntity
{
    public Guid ProfileId { get; set; }
    public string? HospitalId { get; set; }
    public string? SiteId { get; set; }
    public string? DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<WorkflowRuleEntity> Rules { get; set; } = new List<WorkflowRuleEntity>();
}
