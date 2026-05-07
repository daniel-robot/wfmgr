using System.Text.Json;
using Microsoft.OpenApi.Models;
using Wfmgr.Application;
using Wfmgr.Application.Workflows.V1.Config;
using Wfmgr.Api.Workers;
using Wfmgr.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
const string CorsPolicyName = "FrontendCors";

// ── Application & Infrastructure ─────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// TODO: Protect workflow configuration endpoints with admin RBAC before production.
// builder.Services.AddAuthorization(options =>
// {
//     options.AddPolicy(WorkflowConfigPolicies.Admin, policy => policy.RequireClaim("permission", "workflow-config.edit"));
// });

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
});

// ── Background Workers ────────────────────────────────────────────────────────
// OutboxWorker polls every 10 s and dispatches queued PvMed / Monaco messages.
builder.Services.AddHostedService<OutboxWorker>();

var app = builder.Build();

// ── Middleware Pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    // Swagger UI: http://localhost:5223/swagger
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "wfmgr API v1"));
}

// CORS must be placed before UseAuthorization and MapControllers.
app.UseCors(CorsPolicyName);

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
