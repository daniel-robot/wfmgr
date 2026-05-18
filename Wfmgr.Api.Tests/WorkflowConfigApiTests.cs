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
using Wfmgr.Infrastructure.Persistence;
using Xunit;

namespace Wfmgr.Api.Tests;

public class WorkflowConfigApiTests
{
    [Fact]
    public async Task GetSlotCodes_ReturnsKnownSlots()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient("admin");

        var response = await client.GetAsync("/api/workflow-config/slot-codes");
        await PrintResponseForDebugAsync(response);
        var payload = await response.Content.ReadFromJsonAsync<List<WorkflowSlotCodeDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Contains(payload!, x => x.Code == WorkflowSlotCodes.S1ContouringStrategy);
        Assert.Contains(payload!, x => x.Code == WorkflowSlotCodes.S8ExceptionHandlingPolicy);
    }

    [Fact]
    public async Task ValidateRule_WithInvalidSlotCode_ReturnsInvalid()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient("admin");

        var request = new ValidateWorkflowRuleRequest(
            SlotCode: "NOT_A_SLOT",
            ConfigJson: ValidS1ConfigJson,
            ConditionJson: null,
            EffectiveFrom: null,
            EffectiveTo: null,
            Priority: 1);

        var response = await client.PostAsJsonAsync("/api/workflow-config/rules/validate", request);
        var result = await response.Content.ReadFromJsonAsync<ValidateWorkflowRuleResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.False(result!.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("slotCode", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateRule_WithInvalidConfigJson_ReturnsInvalid()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient("admin");

        var request = new ValidateWorkflowRuleRequest(
            SlotCode: WorkflowSlotCodes.S1ContouringStrategy,
            ConfigJson: "{ bad json",
            ConditionJson: null,
            EffectiveFrom: null,
            EffectiveTo: null,
            Priority: 1);

        var response = await client.PostAsJsonAsync("/api/workflow-config/rules/validate", request);
        var result = await response.Content.ReadFromJsonAsync<ValidateWorkflowRuleResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.False(result!.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("configJson is not valid JSON", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateAndUpdateRule_HappyPath_ReturnsOk()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient("admin");

        var profile = await CreateProfileAsync(client, "Happy path profile");

        var createRequest = new CreateWorkflowRuleRequest(
            SlotCode: WorkflowSlotCodes.S1ContouringStrategy,
            Priority: 10,
            Enabled: true,
            ConditionJson: null,
            ConfigJson: ValidS1ConfigJson,
            EffectiveFrom: null,
            EffectiveTo: null,
            ChangeReason: "test create");

        var createResponse = await client.PostAsJsonAsync($"/api/workflow-config/profiles/{profile.Id}/rules", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<WorkflowRuleDto>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);

        var updateRequest = new UpdateWorkflowRuleRequest(
            SlotCode: created!.SlotCode,
            Priority: created.Priority + 1,
            Enabled: created.Enabled,
            ConditionJson: created.ConditionJson,
            ConfigJson: ValidS1ConfigJson,
            EffectiveFrom: created.EffectiveFrom,
            EffectiveTo: created.EffectiveTo,
            ExpectedHash: created.ConcurrencyHash,
            ChangeReason: "test update");

        var updateResponse = await client.PutAsJsonAsync($"/api/workflow-config/rules/{created.Id}", updateRequest);
        var updated = await updateResponse.Content.ReadFromJsonAsync<WorkflowRuleDto>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(createRequest.SlotCode, updated!.SlotCode);
        Assert.Equal(createRequest.Priority + 1, updated.Priority);
    }

    [Fact]
    public async Task UpdateRule_WithStaleExpectedHash_ReturnsConflict()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient("admin");

        var profile = await CreateProfileAsync(client, "Conflict profile");
        var createdRule = await CreateRuleAsync(client, profile.Id);

        var firstUpdate = new UpdateWorkflowRuleRequest(
            SlotCode: createdRule.SlotCode,
            Priority: createdRule.Priority + 1,
            Enabled: createdRule.Enabled,
            ConditionJson: createdRule.ConditionJson,
            ConfigJson: ValidS1ConfigJson,
            EffectiveFrom: createdRule.EffectiveFrom,
            EffectiveTo: createdRule.EffectiveTo,
            ExpectedHash: createdRule.ConcurrencyHash,
            ChangeReason: "first update");

        var firstUpdateResponse = await client.PutAsJsonAsync($"/api/workflow-config/rules/{createdRule.Id}", firstUpdate);
        var updatedRule = await firstUpdateResponse.Content.ReadFromJsonAsync<WorkflowRuleDto>();

        Assert.Equal(HttpStatusCode.OK, firstUpdateResponse.StatusCode);
        Assert.NotNull(updatedRule);

        var staleUpdate = firstUpdate with
        {
            Priority = firstUpdate.Priority + 1,
            ExpectedHash = createdRule.ConcurrencyHash,
            ChangeReason = "stale update"
        };

        var staleResponse = await client.PutAsJsonAsync($"/api/workflow-config/rules/{createdRule.Id}", staleUpdate);
        var conflict = await staleResponse.Content.ReadFromJsonAsync<WorkflowMutationConflictDto>();

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        Assert.NotNull(conflict);
        Assert.Equal(updatedRule!.ConcurrencyHash, conflict!.CurrentHash);
    }

    [Fact]
    public async Task EffectivePreview_ReturnsExplainabilitySections()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient("admin");

        var profile = await CreateProfileAsync(client, "Explainability profile", "H100", "S200", null);
        await CreateRuleAsync(client, profile.Id);

        var response = await client.GetAsync("/api/workflow-config/effective?hospitalId=H100&siteId=S200");
        var payload = await response.Content.ReadFromJsonAsync<EffectiveWorkflowConfigDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.NotNull(payload!.MatchedProfile);
        Assert.NotNull(payload.ResolvedSlots);
        Assert.NotNull(payload.UnmatchedSlots);
        Assert.NotNull(payload.EvaluatedProfiles);
    }

    [Fact]
    public async Task UnauthenticatedRequest_ReturnsUnauthorized()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/workflow-config/slot-codes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NonAdminUser_ReturnsForbidden()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient("SimTech");

        var response = await client.GetAsync("/api/workflow-config/slot-codes");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task PrintResponseForDebugAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            await Console.Out.WriteLineAsync($"=== FAILED: {(int)response.StatusCode} {response.StatusCode} ===");
            await Console.Out.WriteLineAsync(body.Length > 500 ? body[..500] : body);
        }
    }

    private static async Task<WorkflowProfileDto> CreateProfileAsync(
        HttpClient client,
        string name,
        string? hospitalId = "H1",
        string? siteId = "S1",
        string? departmentId = "D1")
    {
        var request = new CreateWorkflowProfileRequest(
            Name: name,
            Version: 1,
            HospitalId: hospitalId,
            SiteId: siteId,
            DepartmentId: departmentId,
            IsActive: true,
            ChangeReason: "integration-test");

        var response = await client.PostAsJsonAsync("/api/workflow-config/profiles", request);
        if (!response.IsSuccessStatusCode)
        {
            await PrintResponseForDebugAsync(response);
        }
        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<WorkflowProfileDto>();
        return profile!;
    }

    private static async Task<WorkflowRuleDto> CreateRuleAsync(HttpClient client, Guid profileId)
    {
        var request = new CreateWorkflowRuleRequest(
            SlotCode: WorkflowSlotCodes.S1ContouringStrategy,
            Priority: 10,
            Enabled: true,
            ConditionJson: null,
            ConfigJson: ValidS1ConfigJson,
            EffectiveFrom: null,
            EffectiveTo: null,
            ChangeReason: "integration-test");

        var response = await client.PostAsJsonAsync($"/api/workflow-config/profiles/{profileId}/rules", request);
        response.EnsureSuccessStatusCode();
        var rule = await response.Content.ReadFromJsonAsync<WorkflowRuleDto>();
        return rule!;
    }

    private const string ValidS1ConfigJson =
        "{\"autoContourEnabled\":true,\"provider\":\"PvMed\",\"onAutoContourComplete\":{\"autoForwardToMonaco\":false,\"allowManualForward\":true},\"fallback\":{\"onFailureCreateManualWorkItem\":true,\"manualWorkItemType\":\"ManualContouring\",\"manualWorkItemRole\":\"Physician\"}}";

    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = $"wfmgr-tests-{Guid.NewGuid():N}";
        private readonly InMemoryDatabaseRoot _dbRoot = new();
        private static readonly string TestSecret = "wfmgr-test-signing-key-at-least-32-chars!!";
        private static readonly SymmetricSecurityKey TestSigningKey = new(Encoding.UTF8.GetBytes(TestSecret));

        public HttpClient CreateAuthenticatedClient(string role = "Admin")
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

            // Set JWT config via builder.UseSetting — these take highest priority
            // and override appsettings.json / appsettings.Development.json values.
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
                    options.UseInMemoryDatabase(_dbName, _dbRoot));
            });
        }
    }
}
