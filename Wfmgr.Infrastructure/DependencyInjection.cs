using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Integrations;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Infrastructure.Integrations;
using Wfmgr.Infrastructure.Persistence;
using Wfmgr.Infrastructure.Profiles;
using Wfmgr.Infrastructure.Persistence.Repositories;

namespace Wfmgr.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("WfmgrDb")
            ?? throw new InvalidOperationException("Connection string 'WfmgrDb' was not found.");

        services.AddDbContext<WfmgrDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IWorkflowCaseRepository, WorkflowCaseRepository>();
        services.AddScoped<IWorkflowDataAccess, WorkflowDataAccess>();
        services.AddScoped<IWorkflowProfileResolver, WorkflowProfileResolver>();
        services.AddHttpClient<IPvMedClient, PvMedClient>(client =>
        {
            var baseUrl = configuration["PvMed:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }
        });
        services.AddScoped<IMonacoAdapter, MonacoAdapter>();

        return services;
    }
}
