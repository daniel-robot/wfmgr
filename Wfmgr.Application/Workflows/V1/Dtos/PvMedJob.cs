namespace Wfmgr.Application.Workflows.V1.Dtos;

public class PvMedJob
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? Progress { get; set; }
}
