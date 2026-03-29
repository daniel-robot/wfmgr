using Microsoft.Extensions.DependencyInjection;
using Wfmgr.Application.Workflows;
using Wfmgr.Application.Workflows.V1.Forms;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.StateMachine;
using Wfmgr.Application.Workflows.V1.WorkItems;

namespace Wfmgr.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IWorkflowCaseService, WorkflowCaseService>();
        services.AddScoped<ICaseWorkflowService, CaseWorkflowService>();
        services.AddScoped<ICaseQueryService, CaseQueryService>();
        services.AddScoped<ICaseFormService, CaseFormService>();
        services.AddScoped<IWorkItemLifecycleService, WorkItemLifecycleService>();
        services.AddScoped<ICaseTransitionGateValidator, CaseTransitionGateValidator>();
        services.AddScoped<ICaseStateMachineService, CaseStateMachineService>();

        return services;
    }
}
