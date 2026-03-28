namespace Wfmgr.Application.Workflows.V1.Dtos;

public class SubmitSimRecordRequest
{
    public string CtMachineId { get; set; } = string.Empty;
    public DateTimeOffset SimulatedAt { get; set; }
    public string RecordFormJson { get; set; } = string.Empty;
}
