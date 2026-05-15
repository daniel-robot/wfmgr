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
using Wfmgr.Application.Workflows.V1.Dtos;
using Wfmgr.Domain.Enums;
using Wfmgr.Domain.WorkItems;
using Wfmgr.Infrastructure.Persistence;
using Xunit;

namespace Wfmgr.Api.Tests;

/// <summary>
/// Focused integration tests for the daily-image-scan flow:
/// CreateCase → SimInProgress (auto) → CompleteDailyImageScan → SimCompleted.
/// Image-stored / SimCompleted → ImageStored is exercised via the existing
/// CT image stored handler and is covered indirectly here.
/// </summary>
public class CaseDailyImageScanFlowTests
{
    [Fact]
    public async Task CreateCase_AutoCreatesDailyImageScanWorkItem_AndAdvancesToSimInProgress()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var caseId = await CreateCaseAsync(client);

        var details = await client.GetFromJsonAsync<CaseDetailsDto>($"/api/cases/{caseId}");
        Assert.NotNull(details);
        Assert.Equal(nameof(CaseStatus.SimInProgress), details!.CurrentStatus);

        var workItems = await client.GetFromJsonAsync<List<WorkItemViewDto>>($"/api/cases/{caseId}/work-items");
        Assert.NotNull(workItems);
        var scan = Assert.Single(workItems!, w => w.Type == WorkItemTypes.DailyImageScan);
        Assert.Equal("XVI", scan.ExternalCorrelationId);
        Assert.Equal(nameof(WorkItemStatus.Pending), scan.Status);
    }

    [Fact]
    public async Task CompleteDailyImageScan_AdvancesCaseToSimCompleted_AndClosesWorkItem()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var caseId = await CreateCaseAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/cases/{caseId}/actions/complete-daily-image-scan",
            new WorkflowActionRequest { TriggeredBy = "simtech-1" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var details = await client.GetFromJsonAsync<CaseDetailsDto>($"/api/cases/{caseId}");
        Assert.Equal(nameof(CaseStatus.SimCompleted), details!.CurrentStatus);

        var workItems = await client.GetFromJsonAsync<List<WorkItemViewDto>>($"/api/cases/{caseId}/work-items");
        var scan = Assert.Single(workItems!, w => w.Type == WorkItemTypes.DailyImageScan);
        Assert.Equal(nameof(WorkItemStatus.Done), scan.Status);
    }

    [Fact]
    public async Task CompleteDailyImageScan_WhenAlreadyCompleted_IsIdempotent()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var caseId = await CreateCaseAsync(client);

        var first = await client.PostAsJsonAsync(
            $"/api/cases/{caseId}/actions/complete-daily-image-scan",
            new WorkflowActionRequest { TriggeredBy = "simtech-1" });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await client.PostAsJsonAsync(
            $"/api/cases/{caseId}/actions/complete-daily-image-scan",
            new WorkflowActionRequest { TriggeredBy = "simtech-1" });
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        var details = await client.GetFromJsonAsync<CaseDetailsDto>($"/api/cases/{caseId}");
        Assert.Equal(nameof(CaseStatus.SimCompleted), details!.CurrentStatus);
    }

    [Fact]
    public async Task CompleteDailyImageScan_WhenCaseMissing_Returns404()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync(
            $"/api/cases/{Guid.NewGuid()}/actions/complete-daily-image-scan",
            new WorkflowActionRequest { TriggeredBy = "simtech-1" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<Guid> CreateCaseAsync(HttpClient client)
    {
        var request = new CreateCaseRequest
        {
            HospitalId = "H1",
            SiteId = "S1",
            DepartmentId = "D1",
            AccessionNumber = $"ACC-{Guid.NewGuid():N}".Substring(0, 16),
            PatientId = "patient-1",
            Notes = "daily image scan flow test"
        };

        var response = await client.PostAsJsonAsync("/api/cases", request);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CreateCaseResponse>();
        Assert.NotNull(payload);
        return payload!.CaseId;
    }

    private sealed record CreateCaseResponse(Guid CaseId);

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = $"wfmgr-tests-{Guid.NewGuid():N}";
        private readonly InMemoryDatabaseRoot _dbRoot = new();
        private static readonly string TestSecret = "wfmgr-test-signing-key-at-least-32-chars!!";
        private static readonly SymmetricSecurityKey TestSigningKey = new(Encoding.UTF8.GetBytes(TestSecret));

        public HttpClient CreateAuthenticatedClient()
        {
            var token = GenerateToken();
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private static string GenerateToken()
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, "test-runner"),
                new(JwtRegisteredClaimNames.Name, "Test Runner"),
                new(ClaimTypes.Role, "SimTech"),
            };

            var credentials = new SigningCredentials(TestSigningKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "wfmgr-dev",
                audience: "wfmgr-api",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials);

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
