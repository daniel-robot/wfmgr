namespace Wfmgr.Application.Workflows;

public sealed record WorkflowCaseDto(
    Guid Id,
    string PatientId,
    string PlanId,
    string Status,
    DateTimeOffset CreatedAtUtc);
