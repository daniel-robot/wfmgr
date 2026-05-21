namespace Wfmgr.Application.Workflows.V1.StateMachine;

/// <summary>
/// Abstraction for workflow subject; allows the state machine to operate on any entity type.
/// </summary>
public interface IWorkflowSubject
{
    Guid CaseId { get; }
    string HospitalId { get; }
    string SiteId { get; }
    string DepartmentId { get; }
    string? PatientId { get; }
    string AccessionNumber { get; }
    int StatusVersion { get; }
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset UpdatedAt { get; }
}
