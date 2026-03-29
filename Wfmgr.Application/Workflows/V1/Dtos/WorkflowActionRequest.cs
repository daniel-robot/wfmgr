namespace Wfmgr.Application.Workflows.V1.Dtos;

public class WorkflowActionRequest
{
    public string TriggeredBy { get; set; } = "ui-user";
    public string? Reason { get; set; }
}
