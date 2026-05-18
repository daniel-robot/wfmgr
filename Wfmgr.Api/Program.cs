using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Wfmgr.Application;
using Wfmgr.Application.Workflows.V1.Config;
using Wfmgr.Api.Auth;
using Wfmgr.Api.Health;
using Wfmgr.Api.Workers;
using Wfmgr.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
const string CorsPolicyName = "FrontendCors";

// ── Application & Infrastructure ─────────────────────────────────────────────
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// ── JWT Settings ──────────────────────────────────────────────────────────────
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));

// ── Authentication ────────────────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
var jwtSecret = jwtSection["Secret"];
var jwtIssuer = jwtSection["Issuer"] ?? "wfmgr";
var jwtAudience = jwtSection["Audience"] ?? "wfmgr-api";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = !string.IsNullOrWhiteSpace(jwtSecret),
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = !string.IsNullOrWhiteSpace(jwtSecret)
            ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
            : null,
        // In production, when using a real identity provider (Azure AD, Auth0, etc.),
        // set Authority instead of Secret and remove IssuerSigningKey / Secret.
    };

    // Map the "role" claim from our JWT to the built-in ClaimTypes.Role
    // so [Authorize(Roles = "...")] works correctly.
    options.MapInboundClaims = false;
});

// ── Authorization — Policy definitions ─────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    // Workflow config admin — requires the "workflow-config.edit" permission claim.
    options.AddPolicy(WorkflowConfigPolicies.Admin, policy =>
        policy.RequireClaim("permission", "workflow-config.edit"));
});

// ── CORS ─────────────────────────────────────────────────────────────────────
// Allowed origins are read from Cors:AllowedOrigins in appsettings.
// Defaults to http://localhost:4200 (Angular dev server) when not configured.
var corsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ── Controllers & JSON ────────────────────────────────────────────────────────
// CamelCase is set explicitly so Angular clients receive consistent property
// names regardless of .NET PascalCase naming conventions on C# types.
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

// ── Swagger ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "wfmgr API",
        Version = "v1",
        Description = "Radiotherapy workflow management API — development testing interface."
    });

    // Add JWT bearer token support to Swagger UI.
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token. Example: \"your-token-here\""
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ── Background Workers ────────────────────────────────────────────────────────
// OutboxWorker polls every 10 s and dispatches qu

// ── Health checks ─────────────────────────────────────────────────────────────
// /health        — liveness (always healthy if process is up)
// /health/ready  — readiness (all checks)
// /health/messaging — messaging only (broker connectivity / publisher mode)
builder.Services.AddHealthChecks()
    .AddCheck<MessagingHealthCheck>("messaging", tags: new[] { "messaging", "ready" });

var app = builder.Build();

// ── Middleware Pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    // Swagger UI: http://localhost:5223/swagger
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "wfmgr API v1"));
}

// CORS must be placed before UseAuthentication / UseAuthorization / MapControllers.
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false, // liveness — process up
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready"),
});
app.MapHealthChecks("/health/messaging", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("messaging"),
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var entry = report.Entries.FirstOrDefault().Value;
        var payload = new
        {
            status = report.Status.ToString(),
            description = entry.Description,
            data = entry.Data,
            duration = report.TotalDuration,
        };
        await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(payload));
    },
});

app.UseCors(CorsPolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
