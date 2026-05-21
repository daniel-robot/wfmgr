using Wfmgr.Engine.Core;

namespace Wfmgr.Engine.Abstractions;

/// <summary>
/// Host-provided pluggable side-effect handler.
/// Registered per named side-effect action key.
/// </summary>
public interface ISideEffectHandler
{
    string ActionName { get; }
    Task ExecuteAsync(SideEffectContext context, CancellationToken ct);
}
