using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Application.Workflows.V1.Gates;

namespace Wfmgr.Application.Workflows.V1.SideEffects;

/// <summary>
/// Execution context passed to <see cref="IWorkflowSideEffectService"/> immediately after
/// a catalog-matched transition succeeds.
/// </summary>
public sealed class SideEffectContext
{
    /// <summary>The mutated case (status already updated by the time side effects run).</summary>
    public required CaseData CaseData { get; init; }

    /// <summary>Gate-validation context supplied by the caller of the transition.</summary>
    public required GateValidationContext ValidationContext { get; init; }

    /// <summary>Timestamp used for all rows written by this execution (defaults to UtcNow).</summary>
    public DateTimeOffset Now { get; init; } = DateTimeOffset.UtcNow;
}
