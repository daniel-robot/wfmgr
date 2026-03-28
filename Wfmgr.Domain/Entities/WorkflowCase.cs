namespace Wfmgr.Domain.Entities;

public class WorkflowCase
{
    public Guid Id { get; set; }

    public string PatientId { get; set; } = string.Empty;

    public string PlanId { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
