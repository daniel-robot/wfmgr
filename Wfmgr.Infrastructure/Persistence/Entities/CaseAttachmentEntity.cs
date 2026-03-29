namespace Wfmgr.Infrastructure.Persistence.Entities;

public class CaseAttachmentEntity
{
    public Guid AttachmentId { get; set; }
    public Guid CaseId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string? SourceSystem { get; set; }
    public string? UploadedBy { get; set; }
    public DateTimeOffset UploadedAt { get; set; }

    public CaseEntity Case { get; set; } = null!;
}