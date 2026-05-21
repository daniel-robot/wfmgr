using Wfmgr.Application.Abstractions.Persistence.Models;
using EngineAbstractions = Wfmgr.Engine.Abstractions;

namespace Wfmgr.Application.EngineAdapters;

/// <summary>
/// Wraps a <see cref="CaseData"/> persistence entity as an engine-level <see cref="EngineAbstractions.IWorkflowSubject"/>.
/// Used internally by adapters to bridge between engine abstractions and host domain types.
/// </summary>
internal sealed class CaseWorkflowSubject : EngineAbstractions.IWorkflowSubject
{
    public CaseWorkflowSubject(CaseData data)
    {
        Data = data;
    }

    public CaseData Data { get; }

    public string SubjectId => Data.CaseId.ToString();
    public string HospitalId => Data.HospitalId;
    public string SiteId => Data.SiteId;
    public string DepartmentId => Data.DepartmentId;
    public string CurrentStatus => Data.CurrentStatus.ToString();
    public int StatusVersion => Data.StatusVersion;
    public DateTimeOffset CreatedAt => Data.CreatedAt;
    public DateTimeOffset UpdatedAt => Data.UpdatedAt;
}
