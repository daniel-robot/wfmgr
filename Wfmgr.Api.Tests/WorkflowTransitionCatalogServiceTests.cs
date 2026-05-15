using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Infrastructure.Persistence;
using Xunit;

namespace Wfmgr.Api.Tests;

/// <summary>
/// Validates that the new DB-backed <see cref="IWorkflowTransitionCatalogService"/>
/// lazy-seeds itself from the static <see cref="WorkflowTransitionCatalog"/> and
/// hydrates rows back into <c>TransitionDefinition</c> objects field-equal to
/// the C# constants.
/// </summary>
public class WorkflowTransitionCatalogServiceTests
{
    [Fact]
    public async Task FirstRead_LazySeedsTable_AndReturnsAllStaticTransitions()
    {
        using var factory = new TestApiFactory();

        // Force boot of the DI container so the test scope is real.
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowTransitionCatalogService>();

        var defs = await catalog.GetAllAsync(CancellationToken.None);

        Assert.Equal(WorkflowTransitionCatalog.All.Count, defs.Count);

        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        Assert.Equal(WorkflowTransitionCatalog.All.Count, await db.WorkflowTransitions.CountAsync());
    }

    [Fact]
    public async Task HydratedDefinitions_AreFieldEqualToStaticCatalog()
    {
        using var factory = new TestApiFactory();
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowTransitionCatalogService>();

        foreach (var expected in WorkflowTransitionCatalog.All)
        {
            var actual = await catalog.FindByCodeAsync(expected.Code, CancellationToken.None);
            Assert.NotNull(actual);
            Assert.Equal(expected.Code, actual!.Code);
            Assert.Equal(expected.ToStatus, actual.ToStatus);
            Assert.Equal(expected.TriggerName, actual.TriggerName);
            Assert.Equal(expected.TriggerType, actual.TriggerType);
            Assert.Equal(expected.ConfigSlot, actual.ConfigSlot);
            Assert.Equal(expected.FromStatuses.OrderBy(s => s), actual.FromStatuses.OrderBy(s => s));
            Assert.Equal(expected.RequiredRoles, actual.RequiredRoles);
            Assert.Equal(expected.GateChecks, actual.GateChecks);
            Assert.Equal(expected.SuccessActions, actual.SuccessActions);
            Assert.Equal(expected.FailureActions, actual.FailureActions);
            Assert.Equal(expected.WorkItemsToCreate, actual.WorkItemsToCreate);
        }
    }

    [Fact]
    public async Task SecondRead_ServesFromCache_AndDoesNotDuplicateRows()
    {
        using var factory = new TestApiFactory();
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowTransitionCatalogService>();

        _ = await catalog.GetAllAsync(CancellationToken.None);
        _ = await catalog.GetAllAsync(CancellationToken.None);
        _ = await catalog.GetAllAsync(CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<WfmgrDbContext>();
        Assert.Equal(WorkflowTransitionCatalog.All.Count, await db.WorkflowTransitions.CountAsync());
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = $"wfmgr-tests-{Guid.NewGuid():N}";
        private readonly InMemoryDatabaseRoot _dbRoot = new();
        private const string TestSecret = "wfmgr-test-signing-key-at-least-32-chars!!";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Authentication:Jwt:Secret", TestSecret);
            builder.UseSetting("Authentication:Jwt:Issuer", "wfmgr-dev");
            builder.UseSetting("Authentication:Jwt:Audience", "wfmgr-api");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();

                var optionsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<WfmgrDbContext>));
                if (optionsDescriptor is not null)
                {
                    services.Remove(optionsDescriptor);
                }

                var contextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(WfmgrDbContext));
                if (contextDescriptor is not null)
                {
                    services.Remove(contextDescriptor);
                }

                services.AddDbContext<WfmgrDbContext>(options =>
                    options.UseInMemoryDatabase(_dbName, _dbRoot)
                        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning)));
            });
        }
    }
}
