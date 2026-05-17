using Microsoft.EntityFrameworkCore;
using Wfmgr.Infrastructure.Integrations.Messaging.Sagas;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence;

public class WfmgrDbContext : DbContext
{
    public WfmgrDbContext(DbContextOptions<WfmgrDbContext> options)
        : base(options)
    {
    }

    public DbSet<CaseEntity> Cases => Set<CaseEntity>();
    public DbSet<PatientEntity> Patients => Set<PatientEntity>();
    public DbSet<WorkItemEntity> WorkItems => Set<WorkItemEntity>();
    public DbSet<ExternalEventEntity> ExternalEvents => Set<ExternalEventEntity>();
    public DbSet<ExternalEventInboxEntity> ExternalEventInbox => Set<ExternalEventInboxEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<WorkflowProfileEntity> WorkflowProfiles => Set<WorkflowProfileEntity>();
    public DbSet<WorkflowRuleEntity> WorkflowRules => Set<WorkflowRuleEntity>();
    public DbSet<WorkflowConfigChangeLogEntity> WorkflowConfigChangeLogs => Set<WorkflowConfigChangeLogEntity>();
    public DbSet<WorkflowTransitionEntity> WorkflowTransitions => Set<WorkflowTransitionEntity>();
    public DbSet<WorkflowTransitionFromStatusEntity> WorkflowTransitionFromStatuses => Set<WorkflowTransitionFromStatusEntity>();
    public DbSet<WorkflowTransitionAttributeEntity> WorkflowTransitionAttributes => Set<WorkflowTransitionAttributeEntity>();
    public DbSet<WorkflowTransitionChangeLogEntity> WorkflowTransitionChangeLogs => Set<WorkflowTransitionChangeLogEntity>();
    public DbSet<WorkflowVocabularyTermEntity> WorkflowVocabularyTerms => Set<WorkflowVocabularyTermEntity>();
    public DbSet<WorkflowVocabularyChangeLogEntity> WorkflowVocabularyChangeLogs => Set<WorkflowVocabularyChangeLogEntity>();
    public DbSet<WorkflowCaseStatusOverlayEntity> WorkflowCaseStatusOverlays => Set<WorkflowCaseStatusOverlayEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<CaseTransitionHistoryEntity> CaseTransitionHistories => Set<CaseTransitionHistoryEntity>();
    public DbSet<CaseFormEntity> CaseForms => Set<CaseFormEntity>();
    public DbSet<CaseAttachmentEntity> CaseAttachments => Set<CaseAttachmentEntity>();
    public DbSet<IntegrationReferenceEntity> IntegrationReferences => Set<IntegrationReferenceEntity>();
    public DbSet<PlanVersionEntity> PlanVersions => Set<PlanVersionEntity>();
    public DbSet<ContouringSagaState> ContouringSagas => Set<ContouringSagaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WfmgrDbContext).Assembly);
    }
}
