using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Domain.Enums;
using Wfmgr.Domain.Forms;
using Wfmgr.Domain.WorkItems;

namespace Wfmgr.Application.Workflows.V1.StateMachine;

public class CaseTransitionGateValidator : ICaseTransitionGateValidator
{
    private readonly IWorkflowDataAccess _dataAccess;

    public CaseTransitionGateValidator(IWorkflowDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    public async Task ValidateAsync(CaseData caseData, TransitionRule rule, TransitionExecutionContext context, CancellationToken ct)
    {
        if (rule.ToStatus == CaseStatus.SimCompleted)
        {
            await EnsureSimulationRecordExistsAsync(caseData.CaseId, ct);
        }

        if (rule.ToStatus == CaseStatus.ImageStored)
        {
            EnsureImageReferenceExists(caseData);
        }

        if (rule.ToStatus == CaseStatus.PlanningPending)
        {
            await EnsureContourReviewApprovalExistsAsync(caseData.CaseId, ct);
        }

        if (rule.ToStatus == CaseStatus.PlanReady || rule.ToStatus == CaseStatus.PlanUnderReview)
        {
            await EnsurePlanVersionExistsAsync(caseData, ct);
        }

        if (rule.ToStatus == CaseStatus.PlanQAInProgress)
        {
            await EnsurePrescriptionExistsAsync(caseData.CaseId, ct);
        }

        if (rule.ToStatus == CaseStatus.ReadyForScheduling)
        {
            await EnsureQaApprovalExistsAsync(caseData.CaseId, ct);
        }

        if (rule.ToStatus == CaseStatus.Treating)
        {
            await EnsureOrderExistsBeforeTreatingAsync(caseData.CaseId, ct);
        }

        if (rule.ToStatus == CaseStatus.Archived)
        {
            await EnsurePostTreatmentReviewExistsAsync(caseData.CaseId, ct);
        }
    }

    private async Task EnsureSimulationRecordExistsAsync(Guid caseId, CancellationToken ct)
    {
        var exists = await _dataAccess.WorkItemExistsAsync(caseId, WorkItemTypes.SimulationRecord, null, ct);
        if (!exists)
        {
            throw new InvalidOperationException("Simulation record must exist before transitioning to SimCompleted.");
        }
    }

    private static void EnsureImageReferenceExists(CaseData caseData)
    {
        var missingImageRef = string.IsNullOrWhiteSpace(caseData.CtStudyInstanceUid)
            || string.IsNullOrWhiteSpace(caseData.CtWadoRsUrl);

        if (missingImageRef)
        {
            throw new InvalidOperationException("Image reference must exist before transitioning to ImageStored.");
        }
    }

    private async Task EnsureContourReviewApprovalExistsAsync(Guid caseId, CancellationToken ct)
    {
        var hasReviewWorkItem = await _dataAccess.WorkItemExistsAsync(caseId, WorkItemTypes.ContourReview, "Approved", ct);
        var hasReviewForm = await _dataAccess.CaseFormExistsAsync(caseId, CaseFormTypes.ContourReviewForm, CaseFormStatuses.Submitted, ct);

        if (!hasReviewWorkItem && !hasReviewForm)
        {
            throw new InvalidOperationException("Contour review approval must exist before transitioning to PlanningPending.");
        }
    }

    private async Task EnsurePlanVersionExistsAsync(CaseData caseData, CancellationToken ct)
    {
        if (caseData.CurrentPlanVersionNo is not null)
        {
            return;
        }

        var exists = await _dataAccess.PlanVersionExistsAsync(caseData.CaseId, ct);
        if (!exists)
        {
            throw new InvalidOperationException("A plan version must exist before transitioning to PlanReady or PlanUnderReview.");
        }
    }

    private async Task EnsurePrescriptionExistsAsync(Guid caseId, CancellationToken ct)
    {
        var hasPrescriptionForm = await _dataAccess.CaseFormExistsAsync(caseId, CaseFormTypes.PlanEvaluationForm, CaseFormStatuses.Submitted, ct);
        var hasPrescriptionSync = await _dataAccess.WorkItemExistsAsync(caseId, WorkItemTypes.PrescriptionSync, "Synced", ct);

        if (!hasPrescriptionForm && !hasPrescriptionSync)
        {
            throw new InvalidOperationException("Prescription must exist before transitioning to PlanQAInProgress.");
        }
    }

    private async Task EnsureQaApprovalExistsAsync(Guid caseId, CancellationToken ct)
    {
        var hasQaWorkItem = await _dataAccess.WorkItemExistsAsync(caseId, WorkItemTypes.PlanQA, "Approved", ct);
        var hasQaForm = await _dataAccess.CaseFormExistsAsync(caseId, CaseFormTypes.PlanQAForm, CaseFormStatuses.Submitted, ct);

        if (!hasQaWorkItem && !hasQaForm)
        {
            throw new InvalidOperationException("QA approval must exist before transitioning to ReadyForScheduling.");
        }
    }

    private async Task EnsureOrderExistsBeforeTreatingAsync(Guid caseId, CancellationToken ct)
    {
        var hasOrderWorkItem = await _dataAccess.WorkItemExistsAsync(caseId, WorkItemTypes.TreatmentOrder, "Submitted", ct);
        if (!hasOrderWorkItem)
        {
            throw new InvalidOperationException("Order must exist before transitioning to Treating.");
        }
    }

    private async Task EnsurePostTreatmentReviewExistsAsync(Guid caseId, CancellationToken ct)
    {
        var hasReviewWorkItem = await _dataAccess.WorkItemExistsAsync(caseId, WorkItemTypes.PostTreatmentReview, "Reviewed", ct);
        var hasReviewForm = await _dataAccess.CaseFormExistsAsync(caseId, CaseFormTypes.PostTreatmentReviewForm, CaseFormStatuses.Submitted, ct);

        if (!hasReviewWorkItem && !hasReviewForm)
        {
            throw new InvalidOperationException("Post-treatment review must exist before transitioning to Archived.");
        }
    }
}
