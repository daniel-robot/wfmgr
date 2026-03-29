namespace Wfmgr.Application.Workflows.V1.Forms.Dtos;

public class CreateCaseFormDraftRequest
{
    public Guid CaseId { get; set; }
    public string FormType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public int? FormVersion { get; set; }
}
