namespace Wfmgr.Application.Workflows.V1.CaseStatuses;

/// <summary>
/// DB-backed cosmetic overlay for the <c>CaseStatus</c> enum. Lazily seeded
/// with one row per enum value on first read; admins can update display
/// metadata but cannot add or remove rows (the enum is the source of truth).
/// </summary>
public interface ICaseStatusOverlayService
{
    Task<IReadOnlyList<CaseStatusOverlayDto>> ListAllAsync(CancellationToken ct);

    Task<CaseStatusOverlayDto?> GetByCodeAsync(string code, CancellationToken ct);

    Task<CaseStatusOverlayMutationResult> UpdateAsync(
        string code, UpdateCaseStatusOverlayRequest request, CancellationToken ct);

    /// <summary>Resets a single overlay row to its in-code defaults.</summary>
    Task<CaseStatusOverlayMutationResult> ResetAsync(string code, CancellationToken ct);

    void InvalidateCache();
}
