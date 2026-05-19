using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wfmgr.Application.Workflows.V1.CaseStatuses;
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Application.Workflows.V1.Vocabulary;
using Wfmgr.Infrastructure.Persistence;
using Wfmgr.Infrastructure.Profiles;

namespace Wfmgr.Infrastructure.Workflows;

/// <summary>
/// Runs every workflow seeder exactly once on application startup so a
/// freshly-migrated empty database is fully populated before the first
/// request is served (rather than each catalog self-seeding lazily on its
/// first read, which can surprise operators and slow down the first call).
/// <para>
/// The underlying <c>EnsureSeededAsync</c> methods on each catalog are
/// idempotent and race-safe, so the lazy paths remain as a fallback for
/// test harnesses that strip <see cref="IHostedService"/>s.
/// </para>
/// </summary>
public sealed class WorkflowSeedingHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowSeedingHostedService> _logger;

    public WorkflowSeedingHostedService(
        IServiceProvider serviceProvider,
        ILogger<WorkflowSeedingHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        // Profile + rule seed: direct call (static seeder, no public read trigger).
        var db = sp.GetRequiredService<WfmgrDbContext>();
        await WorkflowProfileSeeder.EnsureSeededAsync(db, cancellationToken);

        // The remaining seeders are private inside their owning singleton services
        // and run on first read. A single benign read per service triggers them.
        var transitions = sp.GetRequiredService<IWorkflowTransitionCatalogService>();
        await transitions.GetAllAsync(cancellationToken);

        var vocabulary = sp.GetRequiredService<IWorkflowVocabularyCatalogService>();
        await vocabulary.ListAllAsync(cancellationToken);

        var overlays = sp.GetRequiredService<ICaseStatusOverlayService>();
        await overlays.ListAllAsync(cancellationToken);

        _logger.LogInformation(
            "Workflow seeding complete (profiles+rules, transitions, vocabulary, overlays).");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
