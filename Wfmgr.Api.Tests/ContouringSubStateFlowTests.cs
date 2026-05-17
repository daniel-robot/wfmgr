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
/// Integration tests for the granular contouring sub-phase:
/// ImageStored → AutoContouringInProgress → AutoContouringCompleted →
/// ManualContouringInProgress → ManualContouringCompleted → ContoursReady.
/// </summary>
public class ContouringSubStateFlowTests
{
    [Fact]
    public async Task CtImageStored_TransitionsCaseToAutoContouringInProgress_AndCreatesAutoContourMonitor()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var caseId = await CreateCaseAndAdvanceToSimCompletedAsync(client);
        await SendCtImageStoredAsync(client, caseId);

        var details = await client.GetFromJsonAsync<CaseDetailsDto>($"/api/cases/{caseId}");
        Assert.Equal(nameof(CaseStatus.AutoContouringInProgress), details!.CurrentStatus);

        var workItems = await client.GetFromJsonAsync<List<WorkItemViewDto>>($"/api/cases/{caseId}/work-items");
        Assert.Contains(workItems!, w => w.Type == WorkItemTypes.AutoContourMonitor && w.Status == nameof(WorkItemStatus.Pending));
    }

    [Fact]
    public async Task PvMedAutoContourCompleted_AdvancesToManualContouringInProgress_AndCreatesManualWorkItem()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var caseId = await CreateCaseAndAdvanceToSimCompletedAsync(client);
        await SendCtImageStoredAsync(client, caseId);
        await SendPvMedEventAsync(client, caseId, "PVMED_AUTOCONTOUR_COMPLETED");

        var details = await client.GetFromJsonAsync<CaseDetailsDto>($"/api/cases/{caseId}");
        Assert.Equal(nameof(CaseStatus.ManualContouringInProgress), details!.CurrentStatus);

        var workItems = await client.GetFromJsonAsync<List<WorkItemViewDto>>($"/api/cases/{caseId}/work-items");
        Assert.Contains(workItems!, w => w.Type == WorkItemTypes.ManualContouring && w.Status == nameof(WorkItemStatus.Pending));
        Assert.Contains(workItems!, w => w.Type == WorkItemTypes.AutoContourMonitor && w.Status == nameof(WorkItemStatus.Done));
    }

    [Fact]
    public async Task PvMedAutoContourFailed_AdvancesToManualContouringInProgress_AndCreatesManualWorkItem()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var caseId = await CreateCaseAndAdvanceToSimCompletedAsync(client);
        await SendCtImageStoredAsync(client, caseId);
        await SendPvMedEventAsync(client, caseId, "PVMED_AUTOCONTOUR_FAILED");

        var details = await client.GetFromJsonAsync<CaseDetailsDto>($"/api/cases/{caseId}");
        Assert.Equal(nameof(CaseStatus.ManualContouringInProgress), details!.CurrentStatus);

        var workItems = await client.GetFromJsonAsync<List<WorkItemViewDto>>($"/api/cases/{caseId}/work-items");
        Assert.Contains(workItems!, w => w.Type == WorkItemTypes.ManualContouring && w.Status == nameof(WorkItemStatus.Pending));
        Assert.Contains(workItems!, w => w.Type == WorkItemTypes.AutoContourMonitor && w.Status == nameof(WorkItemStatus.Rejected));
    }

    [Fact]
    public async Task CompleteManualContouring_AdvancesAllTheWayToPlanningPending()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var caseId = await CreateCaseAndAdvanceToSimCompletedAsync(client);
        await SendCtImageStoredAsync(client, caseId);
        await SendPvMedEventAsync(client, caseId, "PVMED_AUTOCONTOUR_COMPLETED");

        var response = await client.PostAsync(
            $"/api/cases/{caseId}/actions/complete-manual-contouring",
            content: null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var details = await client.GetFromJsonAsync<CaseDetailsDto>($"/api/cases/{caseId}");
        Assert.Equal(nameof(CaseStatus.PlanningPending), details!.CurrentStatus);

        var history = await client.GetFromJsonAsync<List<TransitionHistoryViewDto>>($"/api/cases/{caseId}/transition-history");
        Assert.Contains(history!, h => h.ToStatus == nameof(CaseStatus.ManualContouringCompleted));
        Assert.Contains(history!, h => h.ToStatus == nameof(CaseStatus.ContoursReady));
        Assert.Contains(history!, h => h.ToStatus == nameof(CaseStatus.PlanningPending));

        var workItems = await client.GetFromJsonAsync<List<WorkItemViewDto>>($"/api/cases/{caseId}/work-items");
        Assert.Contains(workItems!, w => w.Type == WorkItemTypes.ManualContouring && w.Status == nameof(WorkItemStatus.Done));
    }

    [Fact]
    public async Task DuplicatePvMedAutoContourCompleted_IsIdempotent()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var caseId = await CreateCaseAndAdvanceToSimCompletedAsync(client);
        await SendCtImageStoredAsync(client, caseId);
        var externalEventId = Guid.NewGuid().ToString();

        await SendPvMedEventAsync(client, caseId, "PVMED_AUTOCONTOUR_COMPLETED", externalEventId);
        await SendPvMedEventAsync(client, caseId, "PVMED_AUTOCONTOUR_COMPLETED", externalEventId);

        var details = await client.GetFromJsonAsync<CaseDetailsDto>($"/api/cases/{caseId}");
        Assert.Equal(nameof(CaseStatus.ManualContouringInProgress), details!.CurrentStatus);
    }

    [Fact]
    public void Catalog_HasNoTransitionsTargetingRemovedReviewOrReworkStates()
    {
        var obsolete = new[]
        {
            CaseStatus.ContoursUnderReview,
            CaseStatus.ContoursRejected,
            CaseStatus.ContourReworkRequired
        };

        foreach (var t in WorkflowTransitionCatalog.All)
        {
            Assert.DoesNotContain(t.ToStatus, obsolete);
            foreach (var from in t.FromStatuses)
            {
                Assert.DoesNotContain(from, obsolete);
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<Guid> CreateCaseAndAdvanceToSimCompletedAsync(HttpClient client)
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
        var caseId = created!.CaseId;

        // Auto-create flow leaves the case in SimInProgress; complete the daily image scan
        // to reach SimCompleted (precondition for the CT image stored event).
        var complete = await client.PostAsJsonAsync(
            $"/api/cases/{caseId}/actions/complete-daily-image-scan",
            new WorkflowActionRequest { TriggeredBy = "simtech" });
        complete.EnsureSuccessStatusCode();

        return caseId;
    }

    private static async Task SendCtImageStoredAsync(HttpClient client, Guid caseId)
    {
        var details = await client.GetFromJsonAsync<CaseDetailsDto>($"/api/cases/{caseId}");
        var req = new
        {
            externalEventId = Guid.NewGuid().ToString(),
            accessionNumber = details!.AccessionNumber,
            dicomRef = new
            {
                studyInstanceUid = "1.2.3.4.5",
                seriesInstanceUids = new[] { "1.2.3.4.5.1" },
                modality = "CT"
            },
            dicomWebLocation = new { wadoRsUrl = "https://dicom.example.local/wado-rs/studies/1.2.3.4.5" },
            occurredAt = DateTimeOffset.UtcNow.ToString("O")
        };
        var resp = await client.PostAsJsonAsync("/api/integration/ct/image-stored", req);
        resp.EnsureSuccessStatusCode();
    }

    private static async Task SendPvMedEventAsync(HttpClient client, Guid caseId, string type, string? externalEventId = null)
    {
        var req = new
        {
            externalEventId = externalEventId ?? Guid.NewGuid().ToString(),
            caseId,
            type,
            pvMedJob = new { jobId = "job-1", status = "Done", progress = 100 },
            pvMedResult = type == "PVMED_AUTOCONTOUR_COMPLETED"
                ? new
                {
                    rtStructLocation = new
                    {
                        studyInstanceUid = "1.2.3.4.5",
                        seriesInstanceUid = "1.2.3.4.5.99"
                    }
                }
                : null,
            occurredAt = DateTimeOffset.UtcNow.ToString("O")
        };
        var resp = await client.PostAsJsonAsync("/api/integration/pvmed/events", req);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new Exception($"PvMed event {type} failed ({(int)resp.StatusCode}): {body}");
        }
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
                        .UseInternalServiceProvider(new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider())
                        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning)));
            });
        }
    }
}
