namespace Wfmgr.Infrastructure.Persistence.Entities;

public class IntegrationReferenceEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public string SystemName { get; set; } = string.Empty;
    public string ExternalEntityType { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? ExternalStatus { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public CaseEntity Case { get; set; } = null!;
}