using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Integrations;
using Wfmgr.Application.Integrations.Dtos;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Config;
using Wfmgr.Application.Workflows.V1.CaseStatuses;
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Application.Workflows.V1.Vocabulary;
using Wfmgr.Infrastructure.Integrations;
using Wfmgr.Infrastructure.Persistence;
using Wfmgr.Infrastructure.Profiles;
using Wfmgr.Infrastructure.Persistence.Repositories;
using Wfmgr.Infrastructure.Workflows;

namespace Wfmgr.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("WfmgrDb")
            ?? throw new InvalidOperationException("Connection string 'WfmgrDb' was not found.");

        services.AddDbContext<WfmgrDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IWorkflowCaseRepository, WorkflowCaseRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IWorkflowDataAccess, WorkflowDataAccess>();
        services.AddScoped<IWorkflowProfileResolver, WorkflowProfileResolver>();
        services.AddScoped<IWorkflowConfigService, WorkflowConfigService>();
        services.AddSingleton<IWorkflowTransitionCatalogService, WorkflowTransitionCatalogService>();
        services.AddSingleton<IWorkflowVocabularyCatalogService, WorkflowVocabularyCatalogService>();
        services.AddSingleton<ICaseStatusOverlayService, CaseStatusOverlayService>();
        services.AddScoped<IExternalEventDispatcher, ExternalEventDispatcher>();
        services.AddHttpClient<IPvMedClient, PvMedClient>(client =>
        {
            var baseUrl = configuration["PvMed:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }
        });
        services.AddHttpClient<IMsqClient, MsqClient>(client =>
        {
            var baseUrl = configuration["Msq:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }
        });
        services.AddScoped<IMonacoAdapter, MonacoAdapter>();

        return services;
    }
}
