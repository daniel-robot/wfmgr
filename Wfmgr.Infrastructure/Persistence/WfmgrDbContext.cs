using Microsoft.EntityFrameworkCore;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Persistence;

public class WfmgrDbContext : DbContext
{
    public WfmgrDbContext(DbContextOptions<WfmgrDbContext> options)
        : base(options)
    {
    }

    public DbSet<CaseEntity> Cases => Set<CaseEntity>();
    public DbSet<WorkItemEntity> WorkItems => Set<WorkItemEntity>();
    public DbSet<ExternalEventEntity> ExternalEvents => Set<ExternalEventEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<WorkflowProfileEntity> WorkflowProfiles => Set<WorkflowProfileEntity>();
    public DbSet<WorkflowRuleEntity> WorkflowRules => Set<WorkflowRuleEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WfmgrDbContext).Assembly);
    }
}
