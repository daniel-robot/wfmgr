using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
using Wfmgr.Domain;
using Wfmgr.Domain.Enums;
using Wfmgr.Infrastructure.Persistence;
using Xunit;

namespace Wfmgr.Api.Tests;

/// <summary>
/// Integration tests for the Phase 2 admin endpoints — list, create, update,
/// validate, enable/disable, delete, and changelog over the DB-backed workflow
/// transition catalog.
/// </summary>
public class WorkflowTransitionsAdminApiTests
{
    [Fact]
    public async Task List_ReturnsSeededTransitions_WithConcurrencyHash()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var items = await client.GetFromJsonAsync<List<WorkflowTransitionDto>>("/api/workflow-transitions");

        Assert.NotNull(items);
        Assert.Equal(WorkflowTransitionCatalog.All.Count, items!.Count);
        Assert.All(items, i => Assert.False(string.IsNullOrEmpty(i.ConcurrencyHash)));
        Assert.Contains(items, i => i.Code == "SIM-001");
    }

    [Fact]
    public async Task Validate_RejectsUnknownGateCheck_AndUnknownToStatus()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var bad = new CreateWorkflowTransitionRequest(
            Code: "TEST-001",
            Phase: "Other",
            SortOrder: 999,
            ToStatus: "TotallyMadeUpStatus",
            TriggerName: "DoTheThing",
            TriggerType: nameof(WorkflowTriggerType.User),
            ConfigSlot: null,
            Description: null,
            FromStatuses: ["Submitted"],
            RequiredRoles: [WorkflowRoles.Physician],
            GateChecks: ["NotARealGateCheck"],
            SuccessActions: ["Audit"],
            FailureActions: [],
            WorkItemsToCreate: [],
            ChangeReason: null);

        var response = await client.PostAsJsonAsync("/api/workflow-transitions/validate", bad);
        var body = await response.Content.ReadFromJsonAsync<ValidateWorkflowTransitionResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(body!.IsValid);
        Assert.Contains(body.Errors, e => e.Contains("toStatus", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(body.Errors, e => e.Contains("gateCheck", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Create_Update_Disable_RoundTrip_WritesChangeLog()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var create = new CreateWorkflowTransitionRequest(
            Code: "TST-100",
            Phase: "Other",
            SortOrder: 999,
            ToStatus: nameof(CaseStatus.SimScheduled),
            TriggerName: "TestTrigger",
            TriggerType: nameof(WorkflowTriggerType.User),
            ConfigSlot: null,
            Description: "Phase-2 round-trip test transition.",
            FromStatuses: [nameof(CaseStatus.Submitted)],
            RequiredRoles: [WorkflowRoles.Scheduler],
            GateChecks: [],
            SuccessActions: ["Audit"],
            FailureActions: ["StayInSubmitted"],
            WorkItemsToCreate: [],
            ChangeReason: "create");

        var createResp = await client.PostAsJsonAsync("/api/workflow-transitions", create);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<WorkflowTransitionDto>();
        Assert.NotNull(created);

        // Update phase + reason.
        var update = new UpdateWorkflowTransitionRequest(
            Phase: "IntakeSimulation",
            SortOrder: created!.SortOrder,
            ToStatus: created.ToStatus,
            TriggerName: created.TriggerName,
            TriggerType: created.TriggerType,
            ConfigSlot: created.ConfigSlot,
            Description: "updated description",
            FromStatuses: created.FromStatuses,
            RequiredRoles: created.RequiredRoles,
            GateChecks: created.GateChecks,
            SuccessActions: created.SuccessActions,
            FailureActions: created.FailureActions,
            WorkItemsToCreate: created.WorkItemsToCreate,
            ExpectedHash: created.ConcurrencyHash,
            ChangeReason: "update phase");

        var updateResp = await client.PutAsJsonAsync($"/api/workflow-transitions/{created.Id}", update);
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = await updateResp.Content.ReadFromJsonAsync<WorkflowTransitionDto>();
        Assert.Equal("IntakeSimulation", updated!.Phase);
        Assert.NotEqual(created.ConcurrencyHash, updated.ConcurrencyHash);

        // Disable.
        var disableResp = await client.PostAsJsonAsync(
            $"/api/workflow-transitions/{created.Id}/disable",
            new ToggleWorkflowTransitionRequest(updated.ConcurrencyHash, "stop the line"));
        Assert.Equal(HttpStatusCode.OK, disableResp.StatusCode);
        var disabled = await disableResp.Content.ReadFromJsonAsync<WorkflowTransitionDto>();
        Assert.False(disabled!.IsEnabled);

        // Changelog has 3 entries (Create, Update, Disable).
        var changelog = await client.GetFromJsonAsync<List<WorkflowTransitionChangeLogDto>>(
            $"/api/workflow-transitions/{created.Id}/changelog");
        Assert.NotNull(changelog);
        Assert.Equal(3, changelog!.Count);
        Assert.Contains(changelog, c => c.Action == "Create");
        Assert.Contains(changelog, c => c.Action == "Update");
        Assert.Contains(changelog, c => c.Action == "Disable");
    }

    [Fact]
    public async Task Update_WithStaleHash_Returns409Conflict()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        // Pick any seeded transition to mutate.
        var items = await client.GetFromJsonAsync<List<WorkflowTransitionDto>>("/api/workflow-transitions");
        var target = items!.First(i => i.Code == "SIM-002");

        var update = new UpdateWorkflowTransitionRequest(
            Phase: target.Phase,
            SortOrder: target.SortOrder,
            ToStatus: target.ToStatus,
            TriggerName: target.TriggerName,
            TriggerType: target.TriggerType,
            ConfigSlot: target.ConfigSlot,
            Description: "first update",
            FromStatuses: target.FromStatuses,
            RequiredRoles: target.RequiredRoles,
            GateChecks: target.GateChecks,
            SuccessActions: target.SuccessActions,
            FailureActions: target.FailureActions,
            WorkItemsToCreate: target.WorkItemsToCreate,
            ExpectedHash: target.ConcurrencyHash,
            ChangeReason: "first");

        var first = await client.PutAsJsonAsync($"/api/workflow-transitions/{target.Id}", update);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Reuse stale hash → conflict.
        var staleUpdate = update with { Description = "second update", ChangeReason = "stale" };
        var second = await client.PutAsJsonAsync($"/api/workflow-transitions/{target.Id}", staleUpdate);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Meta_ReturnsKnownVocabularies()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var meta = await client.GetFromJsonAsync<WorkflowMetaCatalogDto>("/api/workflow-meta");
        Assert.NotNull(meta);
        Assert.Contains(meta!.CaseStatuses, x => x.Code == nameof(CaseStatus.Submitted));
        Assert.Contains(meta.Roles, x => x.Code == WorkflowRoles.Physician);
        Assert.Contains(meta.GateChecks, x => x.Code == "CaseNotCancelled");
        Assert.Contains(meta.TriggerTypes, x => x.Code == nameof(WorkflowTriggerType.User));
        Assert.NotEmpty(meta.SideEffectActions);
    }

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = $"wfmgr-tests-{Guid.NewGuid():N}";
        private readonly InMemoryDatabaseRoot _dbRoot = new();
        private const string TestSecret = "wfmgr-test-signing-key-at-least-32-chars!!";
        private static readonly SymmetricSecurityKey TestSigningKey = new(Encoding.UTF8.GetBytes(TestSecret));

        public HttpClient CreateAuthenticatedClient()
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", GenerateToken());
            return client;
        }

        private static string GenerateToken()
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, "test-runner"),
                new(JwtRegisteredClaimNames.Name, "Test Runner"),
                new(ClaimTypes.Role, "Admin"),
                new("permission", "workflow-config.edit"),
            };
            var creds = new SigningCredentials(TestSigningKey, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: "wfmgr-dev",
                audience: "wfmgr-api",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

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
                if (optionsDescriptor is not null) services.Remove(optionsDescriptor);

                var contextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(WfmgrDbContext));
                if (contextDescriptor is not null) services.Remove(contextDescriptor);

                services.AddDbContext<WfmgrDbContext>(options =>
                    options.UseInMemoryDatabase(_dbName, _dbRoot)
                        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning)));
            });
        }
    }
}
