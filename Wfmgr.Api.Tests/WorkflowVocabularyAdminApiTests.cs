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
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Application.Workflows.V1.Vocabulary;
using Wfmgr.Domain;
using Wfmgr.Domain.Forms;
using Wfmgr.Domain.WorkItems;
using Wfmgr.Infrastructure.Persistence;
using Xunit;

namespace Wfmgr.Api.Tests;

/// <summary>
/// Integration tests for the Phase 3 workflow-vocabulary admin endpoints.
/// </summary>
public class WorkflowVocabularyAdminApiTests
{
    [Fact]
    public async Task List_ReturnsSeededTermsForAllKinds_AsSystem()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var items = await client.GetFromJsonAsync<List<WorkflowVocabularyTermDto>>("/api/workflow-vocabulary");

        Assert.NotNull(items);
        Assert.NotEmpty(items!);

        Assert.Contains(items, x => x.Kind == WorkflowVocabularyKinds.Role && x.Code == WorkflowRoles.Physician);
        Assert.Contains(items, x => x.Kind == WorkflowVocabularyKinds.WorkItemType && x.Code == WorkItemTypes.DailyImageScan);
        Assert.Contains(items, x => x.Kind == WorkflowVocabularyKinds.CaseFormType && x.Code == CaseFormTypes.PlanQAForm);

        Assert.All(items, i => Assert.True(i.IsSystem));
        Assert.All(items, i => Assert.True(i.IsEnabled));
        Assert.All(items, i => Assert.False(string.IsNullOrEmpty(i.ConcurrencyHash)));
    }

    [Fact]
    public async Task ListByKind_FiltersTerms()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var items = await client.GetFromJsonAsync<List<WorkflowVocabularyTermDto>>(
            $"/api/workflow-vocabulary?kind={WorkflowVocabularyKinds.Role}");

        Assert.NotNull(items);
        Assert.NotEmpty(items!);
        Assert.All(items!, x => Assert.Equal(WorkflowVocabularyKinds.Role, x.Kind));
    }

    [Fact]
    public async Task Create_NewRole_AcceptedByTransitionValidator()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        // Pre-check: validator rejects an unknown role only as a warning (not an error).
        // We use a brand new role so we can be sure it isn't already in the static list.
        var newRole = "NurseNavigator";

        var create = new CreateWorkflowVocabularyTermRequest(
            Kind: WorkflowVocabularyKinds.Role,
            Code: newRole,
            DisplayName: "Nurse Navigator",
            Description: "Coordinator role for special-needs intake",
            SortOrder: null,
            ChangeReason: "phase-3 test");

        var response = await client.PostAsJsonAsync("/api/workflow-vocabulary", create);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<WorkflowVocabularyTermDto>();
        Assert.NotNull(dto);
        Assert.False(dto!.IsSystem);
        Assert.True(dto.IsEnabled);

        // Now validate a transition that uses the new role: validator should NOT
        // emit the unknown-role warning (the role is now in the DB).
        var probe = new CreateWorkflowTransitionRequest(
            Code: "TST-VOC1",
            Phase: "Other",
            SortOrder: 999,
            ToStatus: "SimScheduled",
            TriggerName: "TestVocabRole",
            TriggerType: "User",
            ConfigSlot: null,
            Description: null,
            FromStatuses: ["Submitted"],
            RequiredRoles: [newRole],
            GateChecks: [],
            SuccessActions: ["Audit"],
            FailureActions: [],
            WorkItemsToCreate: [],
            ChangeReason: null);

        var validateResponse = await client.PostAsJsonAsync("/api/workflow-transitions/validate", probe);
        var validation = await validateResponse.Content.ReadFromJsonAsync<ValidateWorkflowTransitionResponse>();
        Assert.NotNull(validation);
        Assert.True(validation!.IsValid);
        Assert.DoesNotContain(validation.Warnings, w => w.Contains(newRole, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SystemTerm_CannotBeDeleted_ButCanBeDisabledAndRenamed()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var all = await client.GetFromJsonAsync<List<WorkflowVocabularyTermDto>>(
            $"/api/workflow-vocabulary?kind={WorkflowVocabularyKinds.Role}");
        var seeded = all!.First(t => t.Code == WorkflowRoles.Scheduler);

        // Delete is rejected (validation 400).
        var deleteResponse = await client.DeleteAsync($"/api/workflow-vocabulary/{seeded.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);

        // Update display name is accepted.
        var update = new UpdateWorkflowVocabularyTermRequest(
            DisplayName: "Treatment Scheduler",
            Description: null,
            SortOrder: null,
            ExpectedHash: seeded.ConcurrencyHash,
            ChangeReason: "rename");
        var updateResponse = await client.PutAsJsonAsync($"/api/workflow-vocabulary/{seeded.Id}", update);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var renamed = await updateResponse.Content.ReadFromJsonAsync<WorkflowVocabularyTermDto>();
        Assert.Equal("Treatment Scheduler", renamed!.DisplayName);
        Assert.NotEqual(seeded.ConcurrencyHash, renamed.ConcurrencyHash);

        // Disable is accepted.
        var disable = new ToggleWorkflowVocabularyTermRequest(renamed.ConcurrencyHash, "deprecate");
        var disableResponse = await client.PostAsJsonAsync($"/api/workflow-vocabulary/{seeded.Id}/disable", disable);
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
        var disabled = await disableResponse.Content.ReadFromJsonAsync<WorkflowVocabularyTermDto>();
        Assert.False(disabled!.IsEnabled);

        // Change log records all three attempted actions (delete is rejected before
        // the log row is written, so we expect Update + Disable = 2 entries).
        var log = await client.GetFromJsonAsync<List<WorkflowVocabularyChangeLogDto>>(
            $"/api/workflow-vocabulary/{seeded.Id}/changelog");
        Assert.NotNull(log);
        Assert.Equal(2, log!.Count);
        Assert.Contains(log, e => e.Action == "Update");
        Assert.Contains(log, e => e.Action == "Disable");
    }

    [Fact]
    public async Task Create_DuplicateCodeForKind_Returns400()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var dup = new CreateWorkflowVocabularyTermRequest(
            Kind: WorkflowVocabularyKinds.Role,
            Code: WorkflowRoles.Physician, // already seeded
            DisplayName: null,
            Description: null,
            SortOrder: null,
            ChangeReason: null);

        var response = await client.PostAsJsonAsync("/api/workflow-vocabulary", dup);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ValidateWorkflowVocabularyTermResponse>();
        Assert.False(body!.IsValid);
        Assert.Contains(body.Errors, e => e.Contains("already exists", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Meta_MergesDbVocabularyWithStaticConstants()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        // Add a brand-new work-item type via the vocab API.
        var add = new CreateWorkflowVocabularyTermRequest(
            Kind: WorkflowVocabularyKinds.WorkItemType,
            Code: "NurseConsult",
            DisplayName: "Nurse consult",
            Description: "Side consult for special-needs cases",
            SortOrder: null,
            ChangeReason: null);

        var addResponse = await client.PostAsJsonAsync("/api/workflow-vocabulary", add);
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);

        var meta = await client.GetFromJsonAsync<WorkflowMetaCatalogDto>("/api/workflow-meta");
        Assert.NotNull(meta);
        // DB-only addition is present in the merged list.
        Assert.Contains(meta!.WorkItemTypes, x => x.Code == "NurseConsult");
        // Static constant remains present.
        Assert.Contains(meta.WorkItemTypes, x => x.Code == WorkItemTypes.DailyImageScan);
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
                        .UseInternalServiceProvider(new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider())
                        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning)));
            });
        }
    }
}
