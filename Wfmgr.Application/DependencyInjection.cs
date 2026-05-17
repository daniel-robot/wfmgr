using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wfmgr.Application.Patients;
using Wfmgr.Application.Workflows;
using Wfmgr.Application.Workflows.V1.Compensation;
using Wfmgr.Application.Workflows.V1.Forms;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Gates;
using Wfmgr.Application.Workflows.V1.Outbox;
using Wfmgr.Application.Workflows.V1.SideEffects;
using Wfmgr.Application.Workflows.V1.StateMachine;
using Wfmgr.Application.Workflows.V1.WorkItems;

namespace Wfmgr.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration? configuration = null)
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

        // Messaging routing policy — reads MessagingOptions to decide which outbox
        // actions are published on the bus vs sent over HTTP. Tests that pass no
        // configuration get an empty BusActions list, i.e. everything stays on HTTP.
        if (configuration is not null)
        {
            services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.SectionName));
        }
        else
        {
            services.Configure<MessagingOptions>(_ => { });
        }
        services.AddSingleton<IOutboxRoutingPolicy, OutboxRoutingPolicy>();

        return services;
    }
}
