using Microsoft.Extensions.DependencyInjection;
using Wfmgr.Engine.Abstractions;
using Wfmgr.Engine.Core;

namespace Wfmgr.Engine;

/// <summary>
/// Registers engine services into the DI container.
/// Hosts call <c>services.AddWorkflowEngine()</c> and then provide their own
/// implementations of all required abstractions.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddWorkflowEngine(this IServiceCollection services)
    {
        services.AddScoped<ITransitionEngine, TransitionEngine>();
        return services;
    }
}
