namespace Wfmgr.Application.Abstractions;

/// <summary>
/// Identity information about the principal performing a workflow operation.
/// Engine-friendly: contains no host-specific (HTTP / JWT) types so the workflow
/// engine can run in any host (Web API, worker, test harness, CLI, etc.).
/// </summary>
public sealed record Actor(string UserId, IReadOnlyCollection<string> Roles)
{
    /// <summary>User-id reserved for system / non-interactive operations.</summary>
    public const string SystemUserId = "system";

    /// <summary>Singleton actor used for internal / hosted-service operations.</summary>
    public static readonly Actor System = new(SystemUserId, new[] { "System" });
}

/// <summary>
/// Strategy for resolving the current <see cref="Actor"/>. Hosts provide an
/// implementation (e.g. one that reads from an HTTP <c>ClaimsPrincipal</c>);
/// engine code depends only on this abstraction.
/// </summary>
public interface IActorAccessor
{
    Actor Current { get; }
}

/// <summary>
/// Default fallback that always resolves to <see cref="Actor.System"/>. Useful
/// for tests, workers, and other non-HTTP hosts.
/// </summary>
public sealed class SystemActorAccessor : IActorAccessor
{
    public Actor Current => Actor.System;
}
