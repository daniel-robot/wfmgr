using Wfmgr.Application.Workflows.V1.Forms.Dtos;

namespace Wfmgr.Application.Workflows.V1.Forms;

public interface ICaseFormService
{
    Task<CaseFormDto> CreateDraftFormAsync(CreateCaseFormDraftRequest request, CancellationToken ct);
    Task<CaseFormDto> UpdateFormPayloadAsync(Guid formId, UpdateCaseFormPayloadRequest request, CancellationToken ct);
    Task<CaseFormDto> SubmitFormAsync(Guid formId, SubmitCaseFormRequest request, CancellationToken ct);
    Task<CaseFormDto?> GetFormByIdAsync(Guid formId, CancellationToken ct);
    Task<IReadOnlyList<CaseFormDto>> GetFormsByCaseAsync(Guid caseId, CancellationToken ct);
    Task<CaseFormDto?> GetLatestFormByCaseAndTypeAsync(Guid caseId, string formType, CancellationToken ct);
    Task ValidateRequiredFormsBeforeTransitionAsync(Guid caseId, Wfmgr.Domain.Enums.CaseStatus toStatus, CancellationToken ct);
}
