using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Domain.Enums;
using EngineAbstractions = Wfmgr.Engine.Abstractions;
using EngineCore = Wfmgr.Engine.Core;

namespace Wfmgr.Application.EngineAdapters;

/// <summary>
/// Implements the engine-level <see cref="EngineAbstractions.IWorkflowDataAccess"/> using the host's
/// application-layer <c>IWorkflowDataAccess</c> persistence abstraction.
/// </summary>
internal sealed class EngineWorkflowDataAccessAdapter : EngineAbstractions.IWorkflowDataAccess
{
    private readonly IWorkflowDataAccess _inner;

    public EngineWorkflowDataAccessAdapter(IWorkflowDataAccess inner)
    {
        _inner = inner;
    }

    public async Task<EngineAbstractions.IWorkflowSubject?> GetSubjectAsync(string subjectId, CancellationToken ct)
    {
        if (!Guid.TryParse(subjectId, out var id))
            return null;

        var caseData = await _inner.GetCaseByIdAsync(id, ct);
        return caseData is not null ? new CaseWorkflowSubject(caseData) : null;
    }

    public async Task UpdateSubjectStatusAsync(EngineAbstractions.IWorkflowSubject subject, string newStatus, int newVersion, CancellationToken ct)
    {
        if (subject is not CaseWorkflowSubject cws)
            throw new NotSupportedException($"Expected CaseWorkflowSubject, got {subject.GetType().Name}");

        var caseData = cws.Data;
        caseData.CurrentStatus = Enum.TryParse<CaseStatus>(newStatus, ignoreCase: true, out var parsed)
            ? parsed
            : caseData.CurrentStatus;
        caseData.StatusVersion = newVersion;
        caseData.UpdatedAt = DateTimeOffset.UtcNow;
        await _inner.UpdateCaseAsync(caseData, ct);
    }

    public async Task AddAuditLogAsync(EngineAbstractions.EngineAuditLogEntry entry, CancellationToken ct)
    {
        var caseId = Guid.Parse(entry.SubjectId);
        await _inner.AddAuditLogAsync(new AuditLogData
        {
            AuditId = Guid.NewGuid(),
            CaseId = caseId,
            ActorType = entry.ActorType,
            ActorId = entry.ActorId,
            Action = entry.Action,
            FromStatus = ParseNullableStatus(entry.FromStatus),
            ToStatus = ParseNullableStatus(entry.ToStatus),
            SnapshotJson = entry.SnapshotJson ?? "{}",
            CreatedAt = entry.CreatedAt,
        }, ct);
    }

    public async Task AddTransitionHistoryAsync(EngineAbstractions.EngineTransitionHistoryEntry entry, CancellationToken ct)
    {
        var caseId = Guid.Parse(entry.SubjectId);
        await _inner.AddCaseTransitionHistoryAsync(new CaseTransitionHistoryData
        {
            TransitionId = Guid.NewGuid(),
            CaseId = caseId,
            FromStatus = entry.FromStatus ?? string.Empty,
            ToStatus = entry.ToStatus,
            TriggerType = entry.TriggerType,
            TriggerName = entry.TriggerName,
            TriggeredBy = entry.TriggeredBy,
            Reason = entry.Reason,
            MetadataJson = entry.MetadataJson ?? "{}",
            CreatedAt = entry.CreatedAt,
        }, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) =>
        _inner.SaveChangesAsync(ct);

    private static CaseStatus? ParseNullableStatus(string? status)
    {
        if (status is null) return null;
        return Enum.TryParse<CaseStatus>(status, ignoreCase: true, out var s) ? s : null;
    }
}
