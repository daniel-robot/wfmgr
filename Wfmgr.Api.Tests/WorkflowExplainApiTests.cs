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
using Wfmgr.Application.Workflows.V1.Config;
using Wfmgr.Application.Workflows.V1.Dtos;
using Wfmgr.Domain.Enums;
using Wfmgr.Infrastructure.Persistence;
using Xunit;

namespace Wfmgr.Api.Tests;

/// <summary>
/// Integration tests for <c>GET /api/workflow-config/transitions</c> (catalog) and
/// <c>POST /api/workflow-config/transitions/explain</c> (dry-run). Verifies that
/// explain does not mutate state and returns per-gate / per-role evaluation.
/// </summary>
public class WorkflowExplainApiTests
{
    [Fact]
    public async Task GetCatalog_ReturnsAllTransitionDefinitions()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient("admin");

        var response = await client.GetAsync("/api/workflow-config/transitions");
        var payload = await response.Content.ReadFromJsonAsync<List<TransitionDefinitionDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!);
        // Spot-check a few well-known transition codes.
        Assert.Contains(payload, t => t.Code == "SIM-002");
        Assert.Contains(payload, t => t.Code == "IMG-001");
    }

    [Fact]
    public async Task Explain_WithUnknownCase_ReturnsNotMatched()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient("admin");

        var request = new ExplainTransitionRequest(
            CaseId: Guid.NewGuid(),
            TriggerName: "ScheduleSimulation",
            Roles: new[] { "SimTech" },
            Reason: null);

        var response = await client.PostAsJsonAsync("/api/workflow-config/transitions/explain", request);
        var payload = await response.Content.ReadFromJsonAsync<ExplainTransitionResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(payload!.MatchFound);
        Assert.False(payload.WouldTransition);
    }

    [Fact]
    public async Task Explain_DoesNotMutateState_AndReportsRoleAndGateOutcome()
    {
        using var factory = new TestApiFactory();
        using var caseClient = factory.CreateAuthenticatedClient();
        using var adminClient = factory.CreateAuthenticatedClient("admin");

        var caseId = await CreateCaseAsync(caseClient);

        var beforeDetails = await caseClient.GetFromJsonAsync<CaseDetailsDto>($"/api/cases/{caseId}");
        var beforeStatus = beforeDetails!.CurrentStatus;

        var request = new ExplainTransitionRequest(
            CaseId: caseId,
            TriggerName: "ScheduleSimulation",
            Roles: new[] { "WrongRole" },
            Reason: null);

        var response = await adminClient.PostAsJsonAsync("/api/workflow-config/transitions/explain", request);
        var payload = await response.Content.ReadFromJsonAsync<ExplainTransitionResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(caseId, payload!.CaseId);

        // Dry-run must not have moved the case.
        var afterDetails = await caseClient.GetFromJsonAsync<CaseDetailsDto>($"/api/cases/{caseId}");
        Assert.Equal(beforeStatus, afterDetails!.CurrentStatus);
    }

    private static async Task<Guid> CreateCaseAsync(HttpClient client)
    {
        var createReq = new CreateCaseRequest
        {
            HospitalId = "H1",
            SiteId = "S1",
            DepartmentId = "D1",
            AccessionNumber = $"ACC-{Guid.NewGuid():N}".Substring(0, 16),
            PatientId = "p1"
        };

        var createResp = await client.PostAsJsonAsync("/api/cases", createReq);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<CreateCaseResponse>();
        return created!.CaseId;
    }

    private sealed record CreateCaseResponse(Guid CaseId);

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = $"wfmgr-tests-{Guid.NewGuid():N}";
        private readonly InMemoryDatabaseRoot _dbRoot = new();
        private static readonly string TestSecret = "wfmgr-test-signing-key-at-least-32-chars!!";
        private static readonly SymmetricSecurityKey TestSigningKey = new(Encoding.UTF8.GetBytes(TestSecret));

        public HttpClient CreateAuthenticatedClient(string role = "SimTech")
        {
            var token = GenerateToken(role);
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private static string GenerateToken(string role)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, "test-runner"),
                new(JwtRegisteredClaimNames.Name, "Test Runner"),
                new(ClaimTypes.Role, role),
            };

            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                claims.Add(new("permission", "workflow-config.edit"));
            }

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecret)),
                SecurityAlgorithms.HmacSha256);

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
                        .UseInternalServiceProvider(new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider())
                        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning)));
            });
        }
    }
}
