namespace Wfmgr.Engine.Abstractions;

/// <summary>
/// Resolves host-specific workflow profile/policy for a given organisational context.
/// The host provides its own implementation with domain-specific policy types.
/// </summary>
public interface IProfileResolver
{
    Task<T> ResolvePolicyAsync<T>(string key, string hospitalId, string siteId, string departmentId, CancellationToken ct);
}
