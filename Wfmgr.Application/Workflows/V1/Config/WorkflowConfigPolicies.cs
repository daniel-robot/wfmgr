using Wfmgr.Application.Abstractions;

namespace Wfmgr.Application.Workflows.V1.Config;

public static class WorkflowConfigPolicies
{
    /// <summary>
    /// Authorization policy name used by workflow-engine admin endpoints.
    /// Aliased to <see cref="WorkflowEngineOptions.AdminPolicyName"/> so the
    /// engine has a single source of truth for the policy identifier.
    /// </summary>
    public const string Admin = WorkflowEngineOptions.AdminPolicyName;
}
