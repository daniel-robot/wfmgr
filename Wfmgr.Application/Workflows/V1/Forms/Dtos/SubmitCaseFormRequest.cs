namespace Wfmgr.Application.Workflows.V1.Forms.Dtos;

public class SubmitCaseFormRequest
{
    public string? PayloadJson { get; set; }
    public string SubmittedBy { get; set; } = "System";
    public string? Reason { get; set; }
}
