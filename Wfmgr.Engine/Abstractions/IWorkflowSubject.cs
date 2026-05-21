namespace Wfmgr.Engine.Abstractions;

/// <summary>
/// Engine-level abstraction for any workflow subject.
/// The host maps its concrete entity to this interface at the boundary.
/// </summary>
public interface IWorkflowSubject
{
    string SubjectId { get; }
    string HospitalId { get; }
    string SiteId { get; }
    string DepartmentId { get; }
    string CurrentStatus { get; }
    int StatusVersion { get; }
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset UpdatedAt { get; }
}
