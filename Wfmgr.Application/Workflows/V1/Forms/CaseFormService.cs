using System.Text.Json;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Application.Workflows.V1.Forms.Dtos;
using Wfmgr.Application.Workflows.V1.StateMachine;
using Wfmgr.Domain.Enums;
using Wfmgr.Domain.Forms;

namespace Wfmgr.Application.Workflows.V1.Forms;

public class CaseFormService : ICaseFormService
{
    private readonly IWorkflowDataAccess _dataAccess;
    private readonly ICaseStateMachineService _caseStateMachineService;
    private readonly ICaseWorkflowService _caseWorkflowService;

    public CaseFormService(
        IWorkflowDataAccess dataAccess,
        ICaseStateMachineService caseStateMachineService,
        ICaseWorkflowService caseWorkflowService)
    {
        _dataAccess = dataAccess;
        _caseStateMachineService = caseStateMachineService;
        _caseWorkflowService = caseWorkflowService;
    }

    public async Task<CaseFormDto> CreateDraftFormAsync(CreateCaseFormDraftRequest request, CancellationToken ct)
    {
        var caseData = await _dataAccess.GetCaseByIdAsync(request.CaseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        ValidateSupportedFormType(request.FormType);

        var latest = await _dataAccess.GetLatestCaseFormByCaseAndTypeAsync(caseData.CaseId, request.FormType, ct);
        var version = request.FormVersion ?? ((latest?.FormVersion ?? 0) + 1);
        var now = DateTimeOffset.UtcNow;

        var data = new CaseFormData
        {
            FormId = Guid.NewGuid(),
            CaseId = caseData.CaseId,
            FormType = request.FormType,
            FormVersion = version,
            Status = CaseFormStatuses.Draft,
            PayloadJson = string.IsNullOrWhiteSpace(request.PayloadJson) ? "{}" : request.PayloadJson,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _dataAccess.AddCaseFormAsync(data, ct);
        await _dataAccess.SaveChangesAsync(ct);

        return Map(data);
    }

    public async Task<CaseFormDto> UpdateFormPayloadAsync(Guid formId, UpdateCaseFormPayloadRequest request, CancellationToken ct)
    {
        var form = await _dataAccess.GetCaseFormByIdAsync(formId, ct)
            ?? throw new InvalidOperationException("Form not found.");

        if (string.Equals(form.Status, CaseFormStatuses.Submitted, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Submitted forms cannot be updated.");
        }

        form.PayloadJson = string.IsNullOrWhiteSpace(request.PayloadJson) ? "{}" : request.PayloadJson;
        form.UpdatedAt = DateTimeOffset.UtcNow;

        await _dataAccess.UpdateCaseFormAsync(form, ct);
        await _dataAccess.SaveChangesAsync(ct);

        return Map(form);
    }

    public async Task<CaseFormDto> SubmitFormAsync(Guid formId, SubmitCaseFormRequest request, CancellationToken ct)
    {
        var form = await _dataAccess.GetCaseFormByIdAsync(formId, ct)
            ?? throw new InvalidOperationException("Form not found.");

        var caseData = await _dataAccess.GetCaseByIdAsync(form.CaseId, ct)
            ?? throw new InvalidOperationException("Case not found.");

        if (!string.IsNullOrWhiteSpace(request.PayloadJson))
        {
            form.PayloadJson = request.PayloadJson;
        }

        form.Status = CaseFormStatuses.Submitted;
        form.SubmittedAt = DateTimeOffset.UtcNow;
        form.SubmittedBy = string.IsNullOrWhiteSpace(request.SubmittedBy) ? "System" : request.SubmittedBy;
        form.UpdatedAt = DateTimeOffset.UtcNow;

        await _dataAccess.UpdateCaseFormAsync(form, ct);
        await ApplyWorkflowEffectsAsync(caseData, form, request, ct);
        await _dataAccess.SaveChangesAsync(ct);

        return Map(form);
    }

    public async Task<CaseFormDto?> GetFormByIdAsync(Guid formId, CancellationToken ct)
    {
        var data = await _dataAccess.GetCaseFormByIdAsync(formId, ct);
        return data is null ? null : Map(data);
    }

    public async Task<IReadOnlyList<CaseFormDto>> GetFormsByCaseAsync(Guid caseId, CancellationToken ct)
    {
        var data = await _dataAccess.GetCaseFormsByCaseIdAsync(caseId, ct);
        return data.Select(Map).ToList();
    }

    public async Task<CaseFormDto?> GetLatestFormByCaseAndTypeAsync(Guid caseId, string formType, CancellationToken ct)
    {
        var data = await _dataAccess.GetLatestCaseFormByCaseAndTypeAsync(caseId, formType, ct);
        return data is null ? null : Map(data);
    }

    public async Task ValidateRequiredFormsBeforeTransitionAsync(Guid caseId, CaseStatus toStatus, CancellationToken ct)
    {
        if (toStatus == CaseStatus.Submitted)
        {
            await EnsureSubmittedFormExistsAsync(caseId, CaseFormTypes.SimulationRequestForm, ct);
        }

        if (toStatus == CaseStatus.SimCompleted)
        {
            await EnsureSubmittedFormExistsAsync(caseId, CaseFormTypes.SimulationRecordForm, ct);
        }

        if (toStatus == CaseStatus.PlanningPending)
        {
            await EnsureSubmittedFormExistsAsync(caseId, CaseFormTypes.ContourReviewForm, ct);
        }

        if (toStatus == CaseStatus.PlanReviewed || toStatus == CaseStatus.PlanningInProgress)
        {
            await EnsureSubmittedFormExistsAsync(caseId, CaseFormTypes.PlanEvaluationForm, ct);
        }

        if (toStatus == CaseStatus.PlanQAApproved || toStatus == CaseStatus.PlanQAFailed)
        {
            await EnsureSubmittedFormExistsAsync(caseId, CaseFormTypes.PlanQAForm, ct);
        }

        if (toStatus == CaseStatus.ReadyForScheduling)
        {
            await EnsureSubmittedFormExistsAsync(caseId, CaseFormTypes.PlanDoubleCheckForm, ct);
        }

        if (toStatus == CaseStatus.OrderSubmitted)
        {
            await EnsureSubmittedFormExistsAsync(caseId, CaseFormTypes.TreatmentOrderForm, ct);
        }

        if (toStatus == CaseStatus.PostTreatmentReviewed || toStatus == CaseStatus.Archived)
        {
            await EnsureSubmittedFormExistsAsync(caseId, CaseFormTypes.PostTreatmentReviewForm, ct);
        }

        if (toStatus == CaseStatus.Cancelled)
        {
            await EnsureSubmittedFormExistsAsync(caseId, CaseFormTypes.CancellationForm, ct);
        }
    }

    private async Task ApplyWorkflowEffectsAsync(CaseData caseData, CaseFormData form, SubmitCaseFormRequest request, CancellationToken ct)
    {
        switch (form.FormType)
        {
            case CaseFormTypes.SimulationRequestForm:
                if (caseData.CurrentStatus == CaseStatus.Draft)
                {
                    await ValidateRequiredFormsBeforeTransitionAsync(caseData.CaseId, CaseStatus.Submitted, ct);
                    await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.Submitted, BuildContext("SubmitCase", WorkflowTriggerType.User, request, form), ct);
                }
                break;

            case CaseFormTypes.SimulationRecordForm:
                if (caseData.CurrentStatus == CaseStatus.SimInProgress)
                {
                    await ValidateRequiredFormsBeforeTransitionAsync(caseData.CaseId, CaseStatus.SimCompleted, ct);
                    await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.SimCompleted, BuildContext("CompleteSimulation", WorkflowTriggerType.User, request, form), ct);
                }
                break;

            case CaseFormTypes.ContourReviewForm:
            {
                if (caseData.CurrentStatus == CaseStatus.ContoursReady)
                {
                    await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.ContoursUnderReview, BuildContext("StartContourReview", WorkflowTriggerType.User, request, form, "Physician"), ct);
                }

                var approved = IsApproved(form.PayloadJson);
                if (approved)
                {
                    await ValidateRequiredFormsBeforeTransitionAsync(caseData.CaseId, CaseStatus.PlanningPending, ct);
                    await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PlanningPending, BuildContext("ApproveContours", WorkflowTriggerType.User, request, form, "Physician"), ct);
                }
                else
                {
                    await _caseWorkflowService.RejectContourReviewAsync(caseData.CaseId, request.Reason ?? "Contour review rejected", request.SubmittedBy, ct);
                }
                break;
            }

            case CaseFormTypes.PlanEvaluationForm:
                if (caseData.CurrentStatus == CaseStatus.PlanUnderReview)
                {
                    if (IsApproved(form.PayloadJson))
                    {
                        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PlanReviewed, BuildContext("ApprovePlan", WorkflowTriggerType.User, request, form, "Physician"), ct);
                    }
                    else
                    {
                        await _caseWorkflowService.RejectPlanReviewAsync(caseData.CaseId, request.Reason ?? "Plan evaluation rejected", request.SubmittedBy, ct);
                    }
                }
                break;

            case CaseFormTypes.PlanReReviewForm:
                if (caseData.CurrentStatus == CaseStatus.PlanReReviewOptional)
                {
                    if (IsApproved(form.PayloadJson))
                    {
                        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PrescriptionGenerating, BuildContext("GeneratePrescriptionAfterRereview", WorkflowTriggerType.User, request, form), ct);
                    }
                    else
                    {
                        await _caseWorkflowService.RejectPlanReReviewAsync(caseData.CaseId, request.Reason ?? "Plan re-review rejected", request.SubmittedBy, ct);
                    }
                }
                break;

            case CaseFormTypes.PlanQAForm:
                if (caseData.CurrentStatus == CaseStatus.PlanQAInProgress)
                {
                    if (IsApproved(form.PayloadJson))
                    {
                        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PlanQAApproved, BuildContext("ApproveQa", WorkflowTriggerType.User, request, form, "Physicist"), ct);
                    }
                    else
                    {
                        await _caseWorkflowService.FailQaAsync(caseData.CaseId, request.Reason ?? "QA failed", request.SubmittedBy, ct);
                    }
                }
                break;

            case CaseFormTypes.PlanDoubleCheckForm:
                if (caseData.CurrentStatus == CaseStatus.PlanDoubleCheckOptional)
                {
                    if (IsApproved(form.PayloadJson))
                    {
                        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.ReadyForScheduling, BuildContext("CompleteDoubleCheck", WorkflowTriggerType.User, request, form), ct);
                    }
                    else
                    {
                        await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PlanningInProgress, BuildContext("DoubleCheckFailed", WorkflowTriggerType.User, request, form), ct);
                    }
                }
                break;

            case CaseFormTypes.TreatmentOrderForm:
                if (caseData.CurrentStatus == CaseStatus.OrderPending)
                {
                    await ValidateRequiredFormsBeforeTransitionAsync(caseData.CaseId, CaseStatus.OrderSubmitted, ct);
                    await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.OrderSubmitted, BuildContext("SubmitOrder", WorkflowTriggerType.User, request, form), ct);
                }
                break;

            case CaseFormTypes.PostTreatmentReviewForm:
                if (caseData.CurrentStatus == CaseStatus.TreatmentCompleted)
                {
                    await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PostTreatmentReviewPending, BuildContext("StartPostTreatmentReview", WorkflowTriggerType.User, request, form), ct);
                }

                if (caseData.CurrentStatus == CaseStatus.PostTreatmentReviewPending)
                {
                    await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.PostTreatmentReviewed, BuildContext("CompletePostTreatmentReview", WorkflowTriggerType.User, request, form), ct);
                }
                break;

            case CaseFormTypes.CancellationForm:
                if (IsTreatingState(caseData.CurrentStatus))
                {
                    await _caseWorkflowService.InterruptTreatmentAsync(caseData.CaseId, request.Reason ?? "Cancelled while in treatment", request.SubmittedBy, ct);
                }
                else
                {
                    await ValidateRequiredFormsBeforeTransitionAsync(caseData.CaseId, CaseStatus.Cancelled, ct);
                    await _caseWorkflowService.CancelCaseAsync(caseData.CaseId, request.Reason ?? "Cancelled by form submission", request.SubmittedBy, ct);
                }
                break;

            default:
                throw new InvalidOperationException($"Unsupported form type '{form.FormType}'.");
        }
    }

    private async Task EnsureSubmittedFormExistsAsync(Guid caseId, string formType, CancellationToken ct)
    {
        var latest = await _dataAccess.GetLatestCaseFormByCaseAndTypeAsync(caseId, formType, ct);
        if (latest is null || !string.Equals(latest.Status, CaseFormStatuses.Submitted, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Submitted form '{formType}' is required.");
        }
    }

    private static bool IsApproved(string payloadJson)
    {
        var token = ReadString(payloadJson, "decision")
            ?? ReadString(payloadJson, "result")
            ?? ReadString(payloadJson, "outcome")
            ?? ReadString(payloadJson, "status");

        return string.Equals(token, "approved", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "approve", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "pass", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "passed", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(string payloadJson, string key)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(payloadJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!doc.RootElement.TryGetProperty(key, out var prop))
        {
            return null;
        }

        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }

    private static void ValidateSupportedFormType(string formType)
    {
        if (SupportedFormTypes.Contains(formType, StringComparer.Ordinal) == false)
        {
            throw new InvalidOperationException($"Form type '{formType}' is not supported.");
        }
    }

    private static readonly string[] SupportedFormTypes =
    [
        CaseFormTypes.SimulationRequestForm,
        CaseFormTypes.SimulationRecordForm,
        CaseFormTypes.ContourReviewForm,
        CaseFormTypes.PlanEvaluationForm,
        CaseFormTypes.PlanReReviewForm,
        CaseFormTypes.PlanQAForm,
        CaseFormTypes.PlanDoubleCheckForm,
        CaseFormTypes.TreatmentOrderForm,
        CaseFormTypes.PostTreatmentReviewForm,
        CaseFormTypes.CancellationForm
    ];

    private static bool IsTreatingState(CaseStatus status)
    {
        return status is CaseStatus.Treating or CaseStatus.TreatmentPaused or CaseStatus.TreatmentInterrupted;
    }

    private static TransitionExecutionContext BuildContext(
        string triggerName,
        WorkflowTriggerType triggerType,
        SubmitCaseFormRequest request,
        CaseFormData form,
        params string[] actorRoles)
    {
        return new TransitionExecutionContext
        {
            TriggerName = triggerName,
            TriggerType = triggerType,
            TriggeredBy = request.SubmittedBy,
            ActorRoles = actorRoles,
            Reason = request.Reason,
            Metadata = new
            {
                form.FormId,
                form.FormType,
                form.FormVersion
            }
        };
    }

    private static CaseFormDto Map(CaseFormData x)
    {
        return new CaseFormDto(
            x.FormId,
            x.CaseId,
            x.FormType,
            x.FormVersion,
            x.Status,
            x.PayloadJson,
            x.SubmittedBy,
            x.SubmittedAt,
            x.CreatedAt,
            x.UpdatedAt);
    }
}
