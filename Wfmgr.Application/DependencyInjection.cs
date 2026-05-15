using Microsoft.Extensions.DependencyInjection;
using Wfmgr.Application.Patients;
using Wfmgr.Application.Workflows;
using Wfmgr.Application.Workflows.V1.Compensation;
using Wfmgr.Application.Workflows.V1.Forms;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Gates;
using Wfmgr.Application.Workflows.V1.SideEffects;
using Wfmgr.Application.Workflows.V1.StateMachine;
using Wfmgr.Application.Workflows.V1.WorkItems;

namespace Wfmgr.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IWorkflowCaseService, WorkflowCaseService>();
        services.AddScoped<ICaseWorkflowService, CaseWorkflowService>();
        services.AddScoped<ICaseQueryService, CaseQueryService>();
        services.AddScoped<ICaseFormService, CaseFormService>();
        services.AddScoped<IWorkItemLifecycleService, WorkItemLifecycleService>();
        services.AddScoped<ICaseStateMachineService, CaseStateMachineService>();
        services.AddScoped<IGateValidationService, GateValidationService>();
        services.AddScoped<IWorkflowSideEffectService, WorkflowSideEffectService>();
        services.AddScoped<ICaseTransitionService, CaseTransitionService>();
        services.AddScoped<IWorkflowCompensationService, WorkflowCompensationService>();
        services.AddScoped<IWorkflowExplainService, WorkflowExplainService>();

        return services;
    }
}
