namespace Wfmgr.Application.Abstractions;

/// <summary>
/// Host-configurable settings for the workflow engine. Binds from the
/// <see cref="SectionName"/> configuration section.
/// </summary>
public sealed class WorkflowEngineOptions
{
    public const string SectionName = "WorkflowEngine";

    /// <summary>
    /// Stable authorization policy name referenced by the engine's
    /// <c>[Authorize(Policy = ...)]</c> attributes on admin endpoints.
    /// <para>
    /// This must remain a compile-time constant because <see cref="object"/>-typed
    /// attribute arguments cannot be sourced from configuration. Hosts customize
    /// the <em>requirements</em> attached to this policy (claim type, claim value)
    /// rather than the policy name itself.
    /// </para>
    /// </summary>
    public const string AdminPolicyName = "WorkflowConfigAdmin";

    /// <summary>
    /// Claim type required by the default admin policy. Defaults to <c>permission</c>.
    /// </summary>
    public string AdminPermissionClaimType { get; set; } = "permission";

    /// <summary>
    /// Claim value required by the default admin policy. Defaults to
    /// <c>workflow-config.edit</c>.
    /// </summary>
    public string AdminPermissionClaimValue { get; set; } = "workflow-config.edit";
}
