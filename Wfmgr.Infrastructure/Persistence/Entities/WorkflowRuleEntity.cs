namespace Wfmgr.Infrastructure.Persistence.Entities;

public class WorkflowRuleEntity
{
    public Guid RuleId { get; set; }
    public Guid ProfileId { get; set; }
    public string SlotCode { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string? ConditionJson { get; set; }
    public string ConfigJson { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public WorkflowProfileEntity Profile { get; set; } = null!;
}
