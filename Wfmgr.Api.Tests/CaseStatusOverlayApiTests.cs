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
using Wfmgr.Application.Workflows.V1.CaseStatuses;
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Domain.Enums;
using Wfmgr.Infrastructure.Persistence;
using Xunit;

namespace Wfmgr.Api.Tests;

/// <summary>
/// Integration tests for the Phase 4 case-status cosmetic overlay endpoints.
/// </summary>
public class CaseStatusOverlayApiTests
{
    [Fact]
    public async Task List_SeedsOneRowPerEnumValue_WithDefaults()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var items = await client.GetFromJsonAsync<List<CaseStatusOverlayDto>>("/api/case-status-overlays");

        Assert.NotNull(items);
        Assert.Equal(Enum.GetValues<CaseStatus>().Length, items!.Count);

        var submitted = items.First(x => x.Code == nameof(CaseStatus.Submitted));
        Assert.Equal((int)CaseStatus.Submitted, submitted.Value);
        Assert.False(string.IsNullOrEmpty(submitted.Color));
        Assert.False(string.IsNullOrEmpty(submitted.Category));
        Assert.False(string.IsNullOrEmpty(submitted.ConcurrencyHash));

        var simScheduled = items.First(x => x.Code == nameof(CaseStatus.SimScheduled));
        Assert.Equal("Simulation", simScheduled.Category);
        Assert.Equal("Sim Scheduled", simScheduled.DisplayName);
    }

    [Fact]
    public async Task Update_PersistsCustomDisplayNameAndColor_AndSurfacesInMeta()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var current = await client.GetFromJsonAsync<CaseStatusOverlayDto>(
            $"/api/case-status-overlays/{nameof(CaseStatus.PlanQAApproved)}");
        Assert.NotNull(current);

        var update = new UpdateCaseStatusOverlayRequest(
            DisplayName: "QA Approved \u2713",
            Description: "Plan passed physics QA verification.",
            Color: "#2e7d32",
            Category: "PlanQA",
            SortOrder: null,
            ExpectedHash: current!.ConcurrencyHash);

        var response = await client.PutAsJsonAsync(
            $"/api/case-status-overlays/{nameof(CaseStatus.PlanQAApproved)}", update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CaseStatusOverlayDto>();
        Assert.Equal("QA Approved \u2713", updated!.DisplayName);
        Assert.Equal("#2e7d32", updated.Color);
        Assert.NotEqual(current.ConcurrencyHash, updated.ConcurrencyHash);

        // Meta endpoint surfaces the custom display name.
        var meta = await client.GetFromJsonAsync<WorkflowMetaCatalogDto>("/api/workflow-meta");
        var metaItem = meta!.CaseStatuses.First(x => x.Code == nameof(CaseStatus.PlanQAApproved));
        Assert.Equal("QA Approved \u2713", metaItem.Description);
    }

    [Fact]
    public async Task Update_StaleHash_Returns409()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var update = new UpdateCaseStatusOverlayRequest(
            DisplayName: "anything",
            Description: null,
            Color: null,
            Category: null,
            SortOrder: null,
            ExpectedHash: "deadbeef-stale-hash");

        var response = await client.PutAsJsonAsync(
            $"/api/case-status-overlays/{nameof(CaseStatus.SimInProgress)}", update);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Reset_RestoresDefaults()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var current = await client.GetFromJsonAsync<CaseStatusOverlayDto>(
            $"/api/case-status-overlays/{nameof(CaseStatus.SimScheduled)}");

        // Mutate
        var update = new UpdateCaseStatusOverlayRequest(
            DisplayName: "CUSTOM-NAME",
            Description: "custom desc",
            Color: "#ff00ff",
            Category: "Custom",
            SortOrder: 999,
            ExpectedHash: current!.ConcurrencyHash);
        var putResp = await client.PutAsJsonAsync(
            $"/api/case-status-overlays/{nameof(CaseStatus.SimScheduled)}", update);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        // Reset
        var resetResp = await client.PostAsync(
            $"/api/case-status-overlays/{nameof(CaseStatus.SimScheduled)}/reset", content: null);
        Assert.Equal(HttpStatusCode.OK, resetResp.StatusCode);
        var reset = await resetResp.Content.ReadFromJsonAsync<CaseStatusOverlayDto>();
        Assert.Equal("Sim Scheduled", reset!.DisplayName);
        Assert.Equal("Simulation", reset.Category);
        Assert.Equal((int)CaseStatus.SimScheduled, reset.SortOrder);
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
