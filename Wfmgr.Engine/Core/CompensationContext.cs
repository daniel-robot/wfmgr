using Wfmgr.Engine.Abstractions;

namespace Wfmgr.Engine.Core;

/// <summary>
/// Context for compensation/rollback operations.
/// </summary>
public class CompensationContext
{
    public IWorkflowSubject Subject { get; set; } = null!;
    public string OriginalStatus { get; set; } = string.Empty;
    public string TriggerName { get; set; } = string.Empty;
    public object? Metadata { get; set; }
}

/// <summary>
/// Result of a compensation operation.
/// </summary>
public class CompensationResult
{
    public bool IsSuccess { get; set; }
    public string? NewStatus { get; set; }
    public string? ErrorMessage { get; set; }

    public static CompensationResult Succeeded(string? newStatus = null) =>
        new() { IsSuccess = true, NewStatus = newStatus };

    public static CompensationResult Failed(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage };
}
