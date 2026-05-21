namespace Wfmgr.Engine;

/// <summary>
/// Host-configurable settings for the workflow engine.
/// </summary>
public sealed class WorkflowEngineOptions
{
    public const string SectionName = "WorkflowEngine";
    public const string AdminPolicyName = "WorkflowConfigAdmin";

    public string AdminPermissionClaimType { get; set; } = "permission";
    public string AdminPermissionClaimValue { get; set; } = "workflow-config.edit";
}
