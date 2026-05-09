using System.Text.Json;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Forms.Dtos;
using Wfmgr.Application.Workflows.V1.StateMachine;
using Wfmgr.Application.Workflows.V1.WorkItems;
using Wfmgr.Domain.Enums;
using Wfmgr.Domain.Forms;
using Wfmgr.Domain.WorkItems;

namespace Wfmgr.Application.Workflows.V1.Forms;

public class CaseFormService : ICaseFormService
{
    private readonly IWorkflowDataAccess _dataAccess;
    private readonly ICaseStateMachineService _caseStateMachineService;
    private readonly ICaseWorkflowService _caseWorkflowService;
    private readonly IWorkflowProfileResolver _profileResolver;
    private readonly IWorkItemLifecycleService _workItemLifecycleService;

    public CaseFormService(
        IWorkflowDataAccess dataAccess,
        ICaseStateMachineService caseStateMachineService,
        ICaseWorkflowService caseWorkflowService,
        IWorkflowProfileResolver profileResolver,
        IWorkItemLifecycleService workItemLifecycleService)
    {
        _dataAccess = dataAccess;
        _caseStateMachineService = caseStateMachineService;
        _caseWorkflowService = caseWorkflowService;
        _profileResolver = profileResolver;
        _workItemLifecycleService = workItemLifecycleService;
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
                // Case starts as Submitted; no Draft→Submitted transition needed.
                break;

            case CaseFormTypes.SimulationRecordForm:
                if (caseData.CurrentStatus == CaseStatus.SimInProgress)
                {
                    await ValidateRequiredFormsBeforeTransitionAsync(caseData.CaseId, CaseStatus.SimCompleted, ct);
                    await _caseStateMachineService.ApplyTransitionAsync(caseData, CaseStatus.SimCompleted, BuildContext("CompleteSimulation", WorkflowTriggerType.User, request, form), ct);
                }
                break;

            case CaseFormTypes.ContourReviewForm:
                // Contour review/rework loop has been removed from the live workflow.
                // The form is still accepted for archival/audit purposes but no
                // longer drives a transition.
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

    private static bool ShouldRequirePlanReReview(string payloadJson, S4PlanReReviewPolicy policy)
    {
        if (!policy.Enabled)
        {
            return false;
        }

        var riskLevels = policy.Trigger.RiskLevelIn;
        if ((riskLevels is null || riskLevels.Length == 0) && policy.Trigger.DoseDeltaPercentGte is null)
        {
            return true;
        }

        var riskLevel = ReadString(payloadJson, "riskLevel");
        if (!string.IsNullOrWhiteSpace(riskLevel) && riskLevels is not null)
        {
            foreach (var configuredLevel in riskLevels)
            {
                if (string.Equals(configuredLevel, riskLevel, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        var doseDelta = ReadDecimal(payloadJson, "doseDeltaPercent");
        if (doseDelta is not null && policy.Trigger.DoseDeltaPercentGte is not null && doseDelta.Value >= policy.Trigger.DoseDeltaPercentGte.Value)
        {
            return true;
        }

        return false;
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

    private static decimal? ReadDecimal(string payloadJson, string key)
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

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var number))
        {
            return number;
        }

        if (prop.ValueKind == JsonValueKind.String && decimal.TryParse(prop.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
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

    private async Task EnsurePlanningDispatchWorkItemAsync(CaseData caseData, CancellationToken ct)
    {
        var openAssignment = await _dataAccess.GetOpenWorkItemAsync(caseData.CaseId, WorkItemTypes.PlanAssignment, ct);
        if (openAssignment is not null)
        {
            return;
        }

        var policy = await _profileResolver.ResolveS3PlanDispatchPolicyAsync(
            caseData.HospitalId,
            caseData.SiteId,
            caseData.DepartmentId,
            ct);

        await _workItemLifecycleService.CreatePendingWorkItemAsync(new CreatePendingWorkItemRequest
        {
            CaseId = caseData.CaseId,
            Type = WorkItemTypes.PlanAssignment,
            AssignedRole = policy.TargetRole,
            SlaMinutes = policy.SlaMinutes,
            PayloadJson = JsonSerializer.Serialize(new
            {
                policy.DispatchMode,
                policy.AllowManualClaim,
                policy.Escalation
            }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, ct);
    }

    private async Task<Guid?> ResolveReferencedWorkItemIdAsync(Guid caseId, string referenceKey, CancellationToken ct)
    {
        var referencedType = referenceKey switch
        {
            "PlanQA" => WorkItemTypes.PlanQA,
            "PlanEvaluation" => WorkItemTypes.PlanEvaluation,
            "PlanDoubleCheck" => WorkItemTypes.PlanDoubleCheck,
            _ => null
        };

        if (referencedType is null)
        {
            return null;
        }

        var workItems = await _dataAccess.GetWorkItemsByCaseIdAsync(caseId, ct);
        var referenced = workItems
            .Where(x => string.Equals(x.Type, referencedType, StringComparison.Ordinal))
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefault();

        return referenced?.WorkItemId;
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
