using Microsoft.Extensions.DependencyInjection;
using Wfmgr.Application.Workflows;
using Wfmgr.Application.Workflows.V1;

namespace Wfmgr.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IWorkflowCaseService, WorkflowCaseService>();
        services.AddScoped<ICaseWorkflowService, CaseWorkflowService>();
        services.AddScoped<ICaseQueryService, CaseQueryService>();

        return services;
    }
}
