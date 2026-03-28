using Wfmgr.Domain.Enums;

namespace Wfmgr.Infrastructure.Persistence.Entities;

public class AuditLogEntity
{
    public Guid AuditId { get; set; }
    public Guid CaseId { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public CaseStatus? FromStatus { get; set; }
    public CaseStatus? ToStatus { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public CaseEntity Case { get; set; } = null!;
}
