using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NSubstitute;
using FluentAssertions;
using Wfmgr.Application.Abstractions.Persistence;
using Wfmgr.Application.Abstractions.Persistence.Models;
using Wfmgr.Application.Workflows.V1;
using Wfmgr.Application.Workflows.V1.Definitions;
using Wfmgr.Application.Workflows.V1.Gates;
using Wfmgr.Application.Workflows.V1.WorkItems;
using Wfmgr.Domain.Enums;
using Wfmgr.Domain.WorkItems;

namespace Wfmgr.Api.Tests;

public sealed class GateValidationServiceTests
{
    private readonly IWorkflowDataAccess _dataAccess = Substitute.For<IWorkflowDataAccess>();
    private readonly IWorkflowProfileResolver _profileResolver = Substitute.For<IWorkflowProfileResolver>();
    private readonly GateValidationService _sut;

    public GateValidationServiceTests()
    {
        _sut = new GateValidationService(_dataAccess, _profileResolver);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CaseData MakeCase(CaseStatus status) => new()
    {
        CaseId = Guid.NewGuid(),
        CurrentStatus = status,
        HospitalId = "HOSP-001",
        SiteId = "SITE-001",
        DepartmentId = "DEPT-001",
        AccessionNumber = "ACC-001",
        PatientId = "PAT-001",
    };

    private static GateValidationContext MakeContext() => new()
    {
        UserId = "user-1",
        Roles = ["Physician"],
        Reason = "Approved"
    };

    private static GateValidationContext MakeEventContext(string source, string type, string externalId)
    {
        return new GateValidationContext
        {
            UserId = "user-1",
            Roles = ["System"],
            Reason = "Event received",
            Metadata = new Dictionary<string, object?>
            {
                [GateCheckNames.MetaEventSource] = source,
                [GateCheckNames.MetaEventType] = type,
                [GateCheckNames.MetaEventExternalId] = externalId
            }
        };
    }

    private static TransitionDefinition MakeTransition(params string[] gateChecks) => new()
    {
        Code = "TEST-001",
        FromStatuses = [CaseStatus.SimScheduled],
        ToStatus = CaseStatus.SimInProgress,
        TriggerName = "TestTrigger",
        TriggerType = WorkflowTriggerType.User,
        GateChecks = gateChecks
    };

    // ── CaseNotCancelledAsync ────────────────────────────────────────────────

    [Fact]
    public async Task CaseNotCancelled_WhenStatusIsCancelled_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.Cancelled);
        var transition = MakeTransition(GateCheckNames.CaseNotCancelled);
        var context = MakeContext();

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeFalse();
        result.FailedChecks.Should().Contain(GateCheckNames.CaseNotCancelled);
    }

    [Fact]
    public async Task CaseNotCancelled_WhenStatusIsActive_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.SimInProgress);
        var transition = MakeTransition(GateCheckNames.CaseNotCancelled);
        var context = MakeContext();

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    // ── CancellationAllowedAsync / TreatmentNotStarted ───────────────────────

    [Theory]
    [InlineData(CaseStatus.SimScheduled)]
    [InlineData(CaseStatus.SimInProgress)]
    [InlineData(CaseStatus.PlanningPending)]
    [InlineData(CaseStatus.PlanQAInProgress)]
    public async Task CancellationAllowed_WhenStatusIsCancellable_ShouldPass(CaseStatus status)
    {
        var caseData = MakeCase(status);
        var transition = MakeTransition(GateCheckNames.CancellationAllowed);
        var context = MakeContext();

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    // ── ImageReferenceExistsAsync ────────────────────────────────────────────

    [Fact]
    public async Task ImageReferenceExists_WhenRefsArePresent_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.SimCompleted);
        caseData.CtStudyInstanceUid = "1.2.3.4.5";
        caseData.CtWadoRsUrl = "https://pacs/wado-rs/1.2.3.4.5";
        var transition = MakeTransition(GateCheckNames.ImageReferenceExists);
        var context = MakeContext();

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ImageReferenceExists_WhenStudyUidIsMissing_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.SimCompleted);
        caseData.CtStudyInstanceUid = "";
        caseData.CtWadoRsUrl = "https://pacs/wado-rs/1.2.3.4.5";
        var transition = MakeTransition(GateCheckNames.ImageReferenceExists);
        var context = MakeContext();

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ImageReferenceExists_WhenWadoUrlIsMissing_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.SimCompleted);
        caseData.CtStudyInstanceUid = "1.2.3.4.5";
        caseData.CtWadoRsUrl = "";
        var transition = MakeTransition(GateCheckNames.ImageReferenceExists);
        var context = MakeContext();

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeFalse();
    }

    // ── ContourResultExistsAsync / ContourResultRefsValid ────────────────────

    [Fact]
    public async Task ContourResultExists_WhenRtStructRefIsPresent_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.ContoursReady);
        caseData.RtStructSeriesInstanceUid = "1.2.3.4.6";
        var transition = MakeTransition(GateCheckNames.ContourResultRefsValid);
        var context = MakeContext();

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ContourResultExists_WhenRtStructRefIsMissing_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.ContoursReady);
        caseData.RtStructSeriesInstanceUid = null;
        var transition = MakeTransition(GateCheckNames.ContourResultRefsValid);
        var context = MakeContext();

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeFalse();
    }

    // ── Simulation checks ────────────────────────────────────────────────────

    [Fact]
    public async Task SimulationScheduleExists_WhenWorkItemExists_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.SimScheduled);
        _dataAccess.WorkItemExistsAsync(
                caseData.CaseId, WorkItemTypes.SimulationSchedule, null, Arg.Any<CancellationToken>())
            .Returns(true);

        var transition = MakeTransition(GateCheckNames.SimulationScheduleExists);
        var result = await _sut.ValidateAsync(caseData, transition, context: MakeContext());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SimulationScheduleExists_WhenMissing_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.SimScheduled);
        _dataAccess.WorkItemExistsAsync(
                caseData.CaseId, WorkItemTypes.SimulationSchedule, null, Arg.Any<CancellationToken>())
            .Returns(false);

        var transition = MakeTransition(GateCheckNames.SimulationScheduleExists);
        var result = await _sut.ValidateAsync(caseData, transition, context: MakeContext());

        result.IsValid.Should().BeFalse();
    }

    // ── SimulationRequestFormValidAsync ──────────────────────────────────────

    [Fact]
    public async Task SimulationRequestFormValid_WhenFormExists_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.SimScheduled);
        _dataAccess.CaseFormExistsAsync(
                caseData.CaseId, Domain.Forms.CaseFormTypes.SimulationRequestForm,
                Domain.Forms.CaseFormStatuses.Submitted, Arg.Any<CancellationToken>())
            .Returns(true);

        var transition = MakeTransition(GateCheckNames.SimulationRequestFormValid);
        var result = await _sut.ValidateAsync(caseData, transition, context: MakeContext());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SimulationRequestFormValid_WhenMissing_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.SimScheduled);
        _dataAccess.CaseFormExistsAsync(
                caseData.CaseId, Domain.Forms.CaseFormTypes.SimulationRequestForm,
                Domain.Forms.CaseFormStatuses.Submitted, Arg.Any<CancellationToken>())
            .Returns(false);

        var transition = MakeTransition(GateCheckNames.SimulationRequestFormValid);
        var result = await _sut.ValidateAsync(caseData, transition, context: MakeContext());

        result.IsValid.Should().BeFalse();
    }

    // ── EventIdempotentAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task EventIdempotent_WhenEventIsNew_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.ContouringInProgress);
        _dataAccess.ExternalEventExistsAsync("PvMed", "AutoContourProgress", "evt-001", Arg.Any<CancellationToken>())
            .Returns(false);

        var transition = MakeTransition(GateCheckNames.EventIdempotent);
        var context = MakeEventContext("PvMed", "AutoContourProgress", "evt-001");
        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task EventIdempotent_WhenEventIsDuplicate_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.ContouringInProgress);
        _dataAccess.ExternalEventExistsAsync("PvMed", "AutoContourProgress", "evt-001", Arg.Any<CancellationToken>())
            .Returns(true);

        var transition = MakeTransition(GateCheckNames.EventIdempotent);
        var context = MakeEventContext("PvMed", "AutoContourProgress", "evt-001");
        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task EventIdempotent_WhenMetadataIsMissing_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.ContouringInProgress);
        var transition = MakeTransition(GateCheckNames.EventIdempotent);
        // Context without event metadata — cannot dedup, allow through
        var context = MakeContext();
        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    // ── ExternalPayloadPresentAsync ──────────────────────────────────────────

    [Fact]
    public async Task ExternalPayloadPresent_WhenPayloadProvided_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.SimCompleted);
        var transition = MakeTransition(GateCheckNames.CaseResolvedByCorrelationKey);
        var context = new GateValidationContext
        {
            UserId = "system",
            Roles = ["System"],
            ExternalEventPayload = "{\"key\": \"value\"}"
        };

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ExternalPayloadPresent_WhenPayloadMissing_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.SimCompleted);
        var transition = MakeTransition(GateCheckNames.CaseResolvedByCorrelationKey);
        var context = new GateValidationContext
        {
            UserId = "system",
            Roles = ["System"],
            ExternalEventPayload = null
        };

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeFalse();
    }

    // ── ReasonPresentAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ReasonPresent_WhenReasonProvided_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.ContoursUnderReview);
        var transition = MakeTransition(GateCheckNames.RejectionReasonRequired);
        var context = new GateValidationContext
        {
            UserId = "physician-1",
            Roles = ["Physician"],
            Reason = "Contours do not match clinical target volume."
        };

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ReasonPresent_WhenReasonEmpty_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.ContoursUnderReview);
        var transition = MakeTransition(GateCheckNames.RejectionReasonRequired);
        var context = new GateValidationContext
        {
            UserId = "physician-1",
            Roles = ["Physician"],
            Reason = string.Empty
        };

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeFalse();
    }

    // ── FormOrPayloadPresentAsync ────────────────────────────────────────────

    [Fact]
    public async Task FormOrPayloadPresent_WhenPayloadProvided_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.PlanReady);
        var transition = MakeTransition(GateCheckNames.PlanPayloadValid);
        var context = new GateValidationContext
        {
            UserId = "dosimetrist-1",
            Roles = ["Dosimetrist"],
            ExternalEventPayload = "{\"plan\": \"data\"}"
        };

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task FormOrPayloadPresent_WhenFormIdProvided_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.PlanReady);
        var transition = MakeTransition(GateCheckNames.PlanPayloadValid);
        var context = new GateValidationContext
        {
            UserId = "dosimetrist-1",
            Roles = ["Dosimetrist"],
            FormId = Guid.NewGuid()
        };

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task FormOrPayloadPresent_WhenBothMissing_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.PlanReady);
        var transition = MakeTransition(GateCheckNames.PlanPayloadValid);
        var context = new GateValidationContext
        {
            UserId = "dosimetrist-1",
            Roles = ["Dosimetrist"],
            FormId = null,
            ExternalEventPayload = null
        };

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeFalse();
    }

    // ── RetryAllowedAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task RetryAllowed_WhenNotExplicitlyDenied_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.ContourReworkRequired);
        var transition = MakeTransition(GateCheckNames.RetryAllowed);
        var context = MakeContext(); // No MetaRetryAllowed in metadata

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task RetryAllowed_WhenExplicitlyDenied_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.ContourReworkRequired);
        var transition = MakeTransition(GateCheckNames.RetryAllowed);
        var context = new GateValidationContext
        {
            UserId = "system",
            Roles = ["System"],
            Metadata = new Dictionary<string, object?> { [GateCheckNames.MetaRetryAllowed] = false }
        };

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeFalse();
    }

    // ── AssigneeExistsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task AssigneeExists_WhenUserIdProvided_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.PlanningPending);
        var transition = MakeTransition(GateCheckNames.AssigneeExists);
        var context = new GateValidationContext
        {
            UserId = "dosimetrist-1",
            Roles = ["Scheduler"]
        };

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task AssigneeExists_WhenUserIdInMetadata_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.PlanningPending);
        var transition = MakeTransition(GateCheckNames.AssigneeExists);
        var context = new GateValidationContext
        {
            UserId = "scheduler-1",
            Roles = ["Scheduler"],
            Metadata = new Dictionary<string, object?>
            {
                [GateCheckNames.MetaAssigneeUserId] = "dosimetrist-1"
            }
        };

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task AssigneeExists_WhenNoUserId_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.PlanningPending);
        var transition = MakeTransition(GateCheckNames.AssigneeExists);
        var context = new GateValidationContext
        {
            Roles = ["Scheduler"],
            UserId = null
        };

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeFalse();
    }

    // ── WorkItemIdPresentAsync ───────────────────────────────────────────────

    [Fact]
    public async Task WorkItemIdPresent_WhenIdProvided_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.PlanningAssigned);
        var transition = MakeTransition(GateCheckNames.TaskAssigned);
        var context = new GateValidationContext
        {
            UserId = "dosimetrist-1",
            Roles = ["Dosimetrist"],
            WorkItemId = Guid.NewGuid()
        };

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task WorkItemIdPresent_WhenIdMissing_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.PlanningAssigned);
        var transition = MakeTransition(GateCheckNames.TaskAssigned);
        var context = new GateValidationContext
        {
            UserId = "dosimetrist-1",
            Roles = ["Dosimetrist"],
            WorkItemId = null
        };

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeFalse();
    }

    // ── PlanVersionExistsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task PlanVersionExists_WhenVersionOnCaseData_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.PlanReady);
        caseData.CurrentPlanVersionNo = 1;
        var transition = MakeTransition(GateCheckNames.PlanVersionExists);
        var context = MakeContext();

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task PlanVersionExists_WhenVersionInDb_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.PlanReady);
        caseData.CurrentPlanVersionNo = null;
        _dataAccess.PlanVersionExistsAsync(caseData.CaseId, Arg.Any<CancellationToken>())
            .Returns(true);

        var transition = MakeTransition(GateCheckNames.PlanVersionExists);
        var context = MakeContext();

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task PlanVersionExists_WhenNoVersion_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.PlanReady);
        caseData.CurrentPlanVersionNo = null;
        _dataAccess.PlanVersionExistsAsync(caseData.CaseId, Arg.Any<CancellationToken>())
            .Returns(false);

        var transition = MakeTransition(GateCheckNames.PlanVersionExists);
        var context = MakeContext();

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeFalse();
    }

    // ── ReviewApprovalExistsAsync / MinimumApprovalsReached ──────────────────

    [Fact]
    public async Task ReviewApprovalExists_WhenWorkItemExists_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.ContoursUnderReview);
        _dataAccess.WorkItemExistsAsync(
                caseData.CaseId, WorkItemTypes.ContourReview, WorkItemResultCodes.Approved, Arg.Any<CancellationToken>())
            .Returns(true);

        var transition = MakeTransition(GateCheckNames.MinimumApprovalsReached);
        var result = await _sut.ValidateAsync(caseData, transition, context: MakeContext());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ReviewApprovalExists_WhenFormExists_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.ContoursUnderReview);
        _dataAccess.WorkItemExistsAsync(
                caseData.CaseId, WorkItemTypes.ContourReview, WorkItemResultCodes.Approved, Arg.Any<CancellationToken>())
            .Returns(false);
        _dataAccess.CaseFormExistsAsync(
                caseData.CaseId, Domain.Forms.CaseFormTypes.ContourReviewForm,
                Domain.Forms.CaseFormStatuses.Submitted, Arg.Any<CancellationToken>())
            .Returns(true);

        var transition = MakeTransition(GateCheckNames.MinimumApprovalsReached);
        var result = await _sut.ValidateAsync(caseData, transition, context: MakeContext());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ReviewApprovalExists_WhenNoApproval_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.ContoursUnderReview);
        _dataAccess.WorkItemExistsAsync(
                caseData.CaseId, WorkItemTypes.ContourReview, WorkItemResultCodes.Approved, Arg.Any<CancellationToken>())
            .Returns(false);
        _dataAccess.CaseFormExistsAsync(
                caseData.CaseId, Domain.Forms.CaseFormTypes.ContourReviewForm,
                Domain.Forms.CaseFormStatuses.Submitted, Arg.Any<CancellationToken>())
            .Returns(false);

        var transition = MakeTransition(GateCheckNames.MinimumApprovalsReached);
        var result = await _sut.ValidateAsync(caseData, transition, context: MakeContext());

        result.IsValid.Should().BeFalse();
    }

    // ── SimulationRecordFormValidAsync ───────────────────────────────────────

    [Fact]
    public async Task SimulationRecordFormValid_WhenFormExists_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.SimInProgress);
        _dataAccess.CaseFormExistsAsync(
                caseData.CaseId, Domain.Forms.CaseFormTypes.SimulationRecordForm,
                Domain.Forms.CaseFormStatuses.Submitted, Arg.Any<CancellationToken>())
            .Returns(true);

        var transition = MakeTransition(GateCheckNames.SimulationRecordFormValid);
        var result = await _sut.ValidateAsync(caseData, transition, context: MakeContext());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task SimulationRecordFormValid_WhenMissing_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.SimInProgress);
        _dataAccess.CaseFormExistsAsync(
                caseData.CaseId, Domain.Forms.CaseFormTypes.SimulationRecordForm,
                Domain.Forms.CaseFormStatuses.Submitted, Arg.Any<CancellationToken>())
            .Returns(false);

        var transition = MakeTransition(GateCheckNames.SimulationRecordFormValid);
        var result = await _sut.ValidateAsync(caseData, transition, context: MakeContext());

        result.IsValid.Should().BeFalse();
    }

    // ── QA approval checks ───────────────────────────────────────────────────

    [Fact]
    public async Task QAApprovalExists_WhenWorkItemExists_ShouldPass()
    {
        var caseData = MakeCase(CaseStatus.PlanQAInProgress);
        _dataAccess.WorkItemExistsAsync(
                caseData.CaseId, WorkItemTypes.PlanQA, WorkItemResultCodes.Approved, Arg.Any<CancellationToken>())
            .Returns(true);

        var transition = MakeTransition(GateCheckNames.QAFormValid);
        var result = await _sut.ValidateAsync(caseData, transition, context: MakeContext());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task QAApprovalExists_WhenMissing_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.PlanQAInProgress);
        _dataAccess.WorkItemExistsAsync(
                caseData.CaseId, WorkItemTypes.PlanQA, WorkItemResultCodes.Approved, Arg.Any<CancellationToken>())
            .Returns(false);
        _dataAccess.CaseFormExistsAsync(
                caseData.CaseId, Domain.Forms.CaseFormTypes.PlanQAForm,
                Domain.Forms.CaseFormStatuses.Submitted, Arg.Any<CancellationToken>())
            .Returns(false);

        var transition = MakeTransition(GateCheckNames.QAFormValid);
        var result = await _sut.ValidateAsync(caseData, transition, context: MakeContext());

        result.IsValid.Should().BeFalse();
    }

    // ── Multiple gate checks ─────────────────────────────────────────────────

    [Fact]
    public async Task MultipleGateChecks_AllPass_ShouldSucceed()
    {
        var caseData = MakeCase(CaseStatus.SimScheduled);
        caseData.CtStudyInstanceUid = "1.2.3.4.5";
        caseData.CtWadoRsUrl = "https://pacs/wado-rs/1.2.3.4.5";
        _dataAccess.WorkItemExistsAsync(
                caseData.CaseId, WorkItemTypes.SimulationSchedule, null, Arg.Any<CancellationToken>())
            .Returns(true);

        var transition = MakeTransition(
            GateCheckNames.CaseActiveNotCancelled,
            GateCheckNames.SimulationScheduleExists,
            GateCheckNames.ImageReferenceExists);
        var context = MakeContext();

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleGateChecks_SomeFail_ShouldCollectAllFailures()
    {
        var caseData = MakeCase(CaseStatus.SimScheduled);
        caseData.CtStudyInstanceUid = null;
        caseData.CtWadoRsUrl = null;
        _dataAccess.WorkItemExistsAsync(
                caseData.CaseId, WorkItemTypes.SimulationSchedule, null, Arg.Any<CancellationToken>())
            .Returns(false);

        var transition = MakeTransition(
            GateCheckNames.CaseActiveNotCancelled,
            GateCheckNames.SimulationScheduleExists,
            GateCheckNames.ImageReferenceExists);
        var context = MakeContext();

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeFalse();
        result.FailedChecks.Should().Contain(GateCheckNames.SimulationScheduleExists);
        result.FailedChecks.Should().Contain(GateCheckNames.ImageReferenceExists);
        // CaseActiveNotCancelled should pass
        result.FailedChecks.Should().NotContain(GateCheckNames.CaseActiveNotCancelled);
    }

    // ── Unknown gate check ───────────────────────────────────────────────────

    [Fact]
    public async Task UnknownGateCheck_ShouldFail()
    {
        var caseData = MakeCase(CaseStatus.Submitted);
        var transition = MakeTransition("UNKNOWN_CHECK_01234");
        var context = MakeContext();

        var result = await _sut.ValidateAsync(caseData, transition, context);

        result.IsValid.Should().BeFalse();
        result.FailedChecks.Should().Contain("UNKNOWN_CHECK_01234");
        result.Messages.Should().Contain(m => m.Contains("not implemented"));
    }
}
