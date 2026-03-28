namespace Wfmgr.Application.Workflows.V1.Dtos;

public class DicomRef
{
    public string StudyInstanceUid { get; set; } = string.Empty;
    public List<string> SeriesInstanceUids { get; set; } = new();
    public string Modality { get; set; } = string.Empty;
}
