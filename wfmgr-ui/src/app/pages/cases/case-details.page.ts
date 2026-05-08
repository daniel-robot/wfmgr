import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { catchError, finalize, forkJoin, of, switchMap } from 'rxjs';
import {
  AuditLogItem,
  CaseAttachmentItem,
  CaseDetails,
  CaseFormItem,
  CtImageStoredRequest,
  ExternalEventItem,
  IntegrationReferenceItem,
  Patient,
  PlanVersionItem,
  PvMedEventRequest,
  TransitionHistoryItem,
  WorkflowOption,
  WorkItem
} from '../../core/models/workflow.models';
import { WorkflowApiService } from '../../core/services/workflow-api.service';

@Component({
  selector: 'app-case-details-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './case-details.page.html',
  styleUrl: './case-details.page.css'
})
export class CaseDetailsPageComponent implements OnInit {
  private readonly api = inject(WorkflowApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  caseId = '';
  details: CaseDetails | null = null;
  patient: Patient | null = null;
  workItems: WorkItem[] = [];
  auditLogs: AuditLogItem[] = [];
  transitionHistory: TransitionHistoryItem[] = [];
  forms: CaseFormItem[] = [];
  attachments: CaseAttachmentItem[] = [];
  externalEvents: ExternalEventItem[] = [];
  integrationReferences: IntegrationReferenceItem[] = [];
  planVersions: PlanVersionItem[] = [];
  workflowStatuses: WorkflowOption[] = [];
  workflowWorkItemTypes: WorkflowOption[] = [];
  loading = false;
  busy = false;
  error = '';
  actionMessage = '';
  infoMessage = '';

  readonly simForm = this.fb.group({
    ctMachineId: ['CT-01', Validators.required],
    simulatedAt: [new Date().toISOString(), Validators.required],
    recordFormJson: ['{"operator":"sim-tech"}', Validators.required]
  });

  readonly ctEventForm = this.fb.group({
    externalEventId: [crypto.randomUUID(), Validators.required],
    accessionNumber: ['', Validators.required],
    studyInstanceUid: ['1.2.840.113619.2.55.3.604688435.1234.1711111111.1', Validators.required],
    seriesInstanceUids: ['1.2.840.113619.2.55.3.604688435.1234.1711111111.2', Validators.required],
    modality: ['CT', Validators.required],
    wadoRsUrl: ['https://dicom.example.local/wado-rs/studies/1.2.3', Validators.required],
    occurredAt: [new Date().toISOString(), Validators.required]
  });

  readonly pvMedForm = this.fb.group({
    externalEventId: [crypto.randomUUID(), Validators.required],
    type: ['PROGRESS', Validators.required],
    jobId: ['pvmed-job-001', Validators.required],
    status: ['Running', Validators.required],
    progress: [55],
    studyInstanceUid: ['1.2.840.113619.2.55.3.604688435.1234.1711111111.1', Validators.required],
    seriesInstanceUid: ['1.2.840.113619.2.55.3.604688435.1234.1711111111.3', Validators.required],
    occurredAt: [new Date().toISOString(), Validators.required]
  });

  readonly caseFormActionForm = this.fb.group({
    formType: ['SimulationRecordForm', Validators.required],
    payloadJson: ['{"source":"ui-test"}', Validators.required],
    submittedBy: ['ui-tester', Validators.required],
    reason: ['UI test submit']
  });

  readonly advancedActionForm = this.fb.group({
    triggeredBy: ['ui-tester', Validators.required],
    reason: ['Manual test action']
  });

  private readonly advancedActionGuards: Record<string, string[]> = {
    'restart-contouring': ['ContourReworkRequired', 'ContoursRejected'],
    'reject-contour-review': ['ContoursUnderReview'],
    'reject-plan-review': ['PlanUnderReview'],
    'reject-plan-rereview': ['PlanReReviewOptional'],
    'prescription-sync-failed': ['PrescriptionGenerating'],
    'retry-prescription-sync': ['PrescriptionSyncFailed'],
    'resolve-prescription-sync': ['PrescriptionSyncFailed'],
    'fail-qa': ['PlanQAInProgress'],
    'scheduling-failed': ['SchedulingInProgress'],
    'retry-scheduling': ['SchedulingInProgress'],
    'pause-treatment': ['Treating'],
    'interrupt-treatment': ['Treating'],
    'resume-treatment': ['TreatmentPaused', 'TreatmentInterrupted'],
    cancel: [
      'Submitted',
      'SimScheduled',
      'SimInProgress',
      'SimCompleted',
      'ImageStored',
      'ImageForwarding',
      'ContouringInProgress',
      'ContoursReady',
      'ContoursUnderReview',
      'ContoursRejected',
      'ContourReworkRequired',
      'PlanningPending',
      'PlanningAssigned',
      'PlanningInProgress',
      'PlanReady',
      'PlanUnderReview',
      'PlanReviewed',
      'PlanReReviewOptional',
      'PrescriptionGenerating',
      'PrescriptionReady',
      'PrescriptionSyncFailed',
      'PlanQAInProgress',
      'PlanQAApproved',
      'PlanQAFailed',
      'PlanDoubleCheckOptional',
      'ReadyForScheduling',
      'SchedulingInProgress',
      'Scheduled',
      'OrderPending',
      'OrderSubmitted',
      'QueuePending'
    ]
  };

  readonly workflowProgressStages = [
    {
      id: 'intake',
      label: 'Intake',
      colorClass: 'stage-intake',
      statuses: ['Submitted']
    },
    {
      id: 'simulation',
      label: 'Simulation',
      colorClass: 'stage-simulation',
      statuses: ['SimScheduled', 'SimInProgress', 'SimCompleted', 'ImageStored', 'ImageForwarding']
    },
    {
      id: 'contouring',
      label: 'Contouring',
      colorClass: 'stage-contouring',
      statuses: ['ContouringInProgress', 'ContoursReady', 'ContoursUnderReview', 'ContoursRejected', 'ContourReworkRequired']
    },
    {
      id: 'planning',
      label: 'Planning',
      colorClass: 'stage-planning',
      statuses: ['PlanningPending', 'PlanningAssigned', 'PlanningInProgress', 'PlanReady', 'PlanUnderReview', 'PlanReviewed', 'PlanReReviewOptional', 'PrescriptionGenerating', 'PrescriptionReady', 'PrescriptionSyncFailed', 'PlanQAInProgress', 'PlanQAApproved', 'PlanQAFailed', 'PlanDoubleCheckOptional']
    },
    {
      id: 'treatment',
      label: 'Treatment',
      colorClass: 'stage-treatment',
      statuses: ['ReadyForScheduling', 'SchedulingInProgress', 'SchedulingFailed', 'Scheduled', 'OrderPending', 'OrderSubmitted', 'QueuePending', 'Treating', 'TreatmentPaused', 'TreatmentInterrupted', 'TreatmentCompleted', 'PostTreatmentReviewPending', 'PostTreatmentReviewed', 'MonacoForwarded', 'Archived', 'Completed', 'Cancelled']
    }
  ];

  ngOnInit(): void {
    this.caseId = this.route.snapshot.paramMap.get('caseId') ?? '';
    if (!this.caseId) {
      this.error = 'Case ID is missing.';
      return;
    }

    this.loadCase();
  }

  loadCase(): void {
    this.loading = true;
    this.error = '';
    this.infoMessage = '';

    this.api
      .getCaseById(this.caseId)
      .pipe(
        switchMap((details) =>
          forkJoin({
            details: of(details),
            patient: details.patientId
              ? this.api.getPatientById(details.patientId).pipe(catchError(() => of(null)))
              : of(null),
            workItems: this.api.getCaseWorkItems(this.caseId).pipe(catchError(() => of([]))),
            auditLogs: this.api.getCaseAuditLogs(this.caseId).pipe(catchError(() => of([]))),
            transitionHistory: this.api.getCaseTransitionHistory(this.caseId).pipe(catchError(() => of([]))),
            forms: this.api.getCaseForms(this.caseId).pipe(catchError(() => of([]))),
            attachments: this.api.getCaseAttachments(this.caseId).pipe(catchError(() => of([]))),
            externalEvents: this.api.getCaseExternalEvents(this.caseId).pipe(catchError(() => of([]))),
            integrationReferences: this.api.getCaseIntegrationReferences(this.caseId).pipe(catchError(() => of([]))),
            planVersions: this.api.getCasePlanVersions(this.caseId).pipe(catchError(() => of([]))),
            workflowStatuses: this.api.getWorkflowStatuses().pipe(catchError(() => of([]))),
            workflowWorkItemTypes: this.api.getWorkflowWorkItemTypes().pipe(catchError(() => of([])))
          })
        ),
        finalize(() => (this.loading = false))
      )
      .subscribe({
        next: ({
          details,
          patient,
          workItems,
          auditLogs,
          transitionHistory,
          forms,
          attachments,
          externalEvents,
          integrationReferences,
          planVersions,
          workflowStatuses,
          workflowWorkItemTypes
        }) => {
          this.details = details;
          this.patient = patient;
          this.workItems = workItems;
          this.auditLogs = auditLogs;
          this.transitionHistory = transitionHistory;
          this.forms = forms;
          this.attachments = attachments;
          this.externalEvents = externalEvents;
          this.integrationReferences = integrationReferences;
          this.planVersions = planVersions;
          this.workflowStatuses = workflowStatuses;
          this.workflowWorkItemTypes = workflowWorkItemTypes;
          this.infoMessage = `Refreshed at ${new Date().toLocaleTimeString()}.`;
          this.ctEventForm.patchValue({ accessionNumber: details.accessionNumber });
        },
        error: (err) => (this.error = err?.error?.message ?? 'Failed to load case details.')
      });
  }

  get pendingWorkItems(): WorkItem[] {
    return this.workItems.filter((w) => w.status === 'Pending');
  }

  submitSimRecord(): void {
    if (this.simForm.invalid || !this.caseId) {
      this.simForm.markAllAsTouched();
      return;
    }

    this.runAction(() =>
      this.api.submitSimRecord(this.caseId, {
        ctMachineId: this.simForm.value.ctMachineId!,
        simulatedAt: this.simForm.value.simulatedAt!,
        recordFormJson: this.simForm.value.recordFormJson!
      })
    );
  }

  simulateCtImageStored(): void {
    if (!this.isCtEventEnabled()) {
      this.error = this.getCtEventHint();
      return;
    }

    if (this.ctEventForm.invalid) {
      this.ctEventForm.markAllAsTouched();
      return;
    }

    const freshEventId = crypto.randomUUID();
    this.ctEventForm.patchValue({ externalEventId: freshEventId });

    const req: CtImageStoredRequest = {
      externalEventId: freshEventId,
      accessionNumber: this.ctEventForm.value.accessionNumber!,
      dicomRef: {
        studyInstanceUid: this.ctEventForm.value.studyInstanceUid!,
        seriesInstanceUids: this.ctEventForm.value.seriesInstanceUids!.split(',').map((x) => x.trim()),
        modality: this.ctEventForm.value.modality!
      },
      dicomWebLocation: {
        wadoRsUrl: this.ctEventForm.value.wadoRsUrl!
      },
      occurredAt: this.ctEventForm.value.occurredAt!
    };

    this.runAction(() => this.api.simulateCtImageStored(req));
  }

  isCtEventEnabled(): boolean {
    return this.details?.currentStatus === 'SimCompleted';
  }

  getCtEventHint(): string {
    const status = this.details?.currentStatus;

    if (!status) {
      return 'Case status is unavailable. Refresh and try again.';
    }

    if (status === 'SimCompleted') {
      return 'Ready: sending CT event will move the case into contouring.';
    }

    if (
      status === 'ImageStored' ||
      status === 'ImageForwarding' ||
      status === 'ContouringInProgress' ||
      status === 'ContoursReady' ||
      status === 'ContoursUnderReview' ||
      status === 'ContoursRejected' ||
      status === 'ContourReworkRequired' ||
      status === 'PlanningPending'
    ) {
      return `CT image event already processed for this case (current status: ${status}).`;
    }

    return `CT event can be sent only when status is SimCompleted (current status: ${status}). Submit Sim Record first.`;
  }

  simulatePvMedEvent(type: string): void {
    if (this.pvMedForm.invalid || !this.caseId) {
      this.pvMedForm.markAllAsTouched();
      return;
    }

    this.pvMedForm.patchValue({ type });

    const req: PvMedEventRequest = {
      externalEventId: this.pvMedForm.value.externalEventId!,
      caseId: this.caseId,
      type,
      pvMedJob: {
        jobId: this.pvMedForm.value.jobId!,
        status: this.pvMedForm.value.status!,
        progress: this.pvMedForm.value.progress
      },
      pvMedResult:
        type === 'PVMED_AUTOCONTOUR_COMPLETED'
          ? {
              rtStructLocation: {
                studyInstanceUid: this.pvMedForm.value.studyInstanceUid!,
                seriesInstanceUid: this.pvMedForm.value.seriesInstanceUid!
              }
            }
          : null,
      occurredAt: this.pvMedForm.value.occurredAt!
    };

    this.runAction(() => this.api.simulatePvMedEvent(req));
  }

  forwardToMonaco(): void {
    if (!this.caseId) {
      return;
    }

    this.runAction(() => this.api.forwardToMonaco(this.caseId));
  }

  completeManualContouring(): void {
    if (!this.caseId) {
      return;
    }

    this.runAction(() => this.api.completeManualContouring(this.caseId));
  }

  createAndSubmitForm(): void {
    if (this.caseFormActionForm.invalid || !this.caseId) {
      this.caseFormActionForm.markAllAsTouched();
      return;
    }

    const formType = this.caseFormActionForm.value.formType!;
    const payloadJson = this.caseFormActionForm.value.payloadJson!;
    const submittedBy = this.caseFormActionForm.value.submittedBy!;
    const reason = this.caseFormActionForm.value.reason || undefined;

    this.runAction(() =>
      this.api
        .createCaseFormDraft(this.caseId, {
          formType,
          payloadJson,
          formVersion: 1
        })
        .pipe(
          switchMap((draft) =>
            this.api.submitCaseForm(this.caseId, draft.formId, {
              payloadJson,
              submittedBy,
              reason
            })
          )
        )
    );
  }

  runAdvancedAction(action: string): void {
    if (this.advancedActionForm.invalid || !this.caseId) {
      this.advancedActionForm.markAllAsTouched();
      return;
    }

    if (!this.isActionEnabled(action)) {
      this.error = `Action '${action}' is not allowed for status '${this.details?.currentStatus ?? 'Unknown'}'. Allowed: ${this.getAllowedStatusesText(action)}.`;
      return;
    }

    const request = {
      triggeredBy: this.advancedActionForm.value.triggeredBy!,
      reason: this.advancedActionForm.value.reason || undefined
    };

    switch (action) {
      case 'restart-contouring':
        this.runAction(() => this.api.restartContouring(this.caseId, request));
        break;
      case 'reject-contour-review':
        this.runAction(() => this.api.rejectContourReview(this.caseId, request));
        break;
      case 'reject-plan-review':
        this.runAction(() => this.api.rejectPlanReview(this.caseId, request));
        break;
      case 'reject-plan-rereview':
        this.runAction(() => this.api.rejectPlanReReview(this.caseId, request));
        break;
      case 'prescription-sync-failed':
        this.runAction(() => this.api.markPrescriptionSyncFailed(this.caseId, request));
        break;
      case 'retry-prescription-sync':
        this.runAction(() => this.api.retryPrescriptionSync(this.caseId, request));
        break;
      case 'resolve-prescription-sync':
        this.runAction(() => this.api.resolvePrescriptionSync(this.caseId, request));
        break;
      case 'fail-qa':
        this.runAction(() => this.api.failQa(this.caseId, request));
        break;
      case 'scheduling-failed':
        this.runAction(() => this.api.markSchedulingFailed(this.caseId, request));
        break;
      case 'retry-scheduling':
        this.runAction(() => this.api.retryScheduling(this.caseId, request));
        break;
      case 'pause-treatment':
        this.runAction(() => this.api.pauseTreatment(this.caseId, request));
        break;
      case 'interrupt-treatment':
        this.runAction(() => this.api.interruptTreatment(this.caseId, request));
        break;
      case 'resume-treatment':
        this.runAction(() => this.api.resumeTreatment(this.caseId, request));
        break;
      case 'cancel':
        this.runAction(() => this.api.cancelCase(this.caseId, request));
        break;
      default:
        this.error = `Unknown action '${action}'.`;
        break;
    }
  }

  isActionEnabled(action: string): boolean {
    const currentStatus = this.details?.currentStatus;
    if (!currentStatus) {
      return false;
    }

    const allowedStatuses = this.advancedActionGuards[action];
    if (!allowedStatuses || allowedStatuses.length === 0) {
      return false;
    }

    return allowedStatuses.includes(currentStatus);
  }

  getAllowedStatusesText(action: string): string {
    const allowedStatuses = this.advancedActionGuards[action] ?? [];
    return allowedStatuses.length > 0 ? allowedStatuses.join(', ') : 'None';
  }

  private runAction(work: () => import('rxjs').Observable<unknown>): void {
    this.busy = true;
    this.actionMessage = '';
    this.error = '';

    work()
      .pipe(finalize(() => (this.busy = false)))
      .subscribe({
        next: () => {
          this.actionMessage = 'Action submitted successfully.';
          this.ctEventForm.patchValue({ externalEventId: crypto.randomUUID() });
          this.pvMedForm.patchValue({ externalEventId: crypto.randomUUID() });
          this.loadCase();
        },
        error: (err) => (this.error = err?.error?.message ?? 'Action failed.')
      });
  }

  getStatusClass(status: string | null | undefined): string {
    if (!status) {
      return '';
    }

    return `status-${status.toLowerCase()}`;
  }

  getWorkflowStageState(status: string | null | undefined, stageIndex: number): 'completed' | 'current' | 'upcoming' {
    const current = this.getCurrentWorkflowStageIndex(status);
    if (stageIndex < current) return 'completed';
    if (stageIndex === current) return 'current';
    return 'upcoming';
  }

  getCurrentWorkflowStageIndex(status: string | null | undefined): number {
    const normalizedStatus = status ?? '';
    const idx = this.workflowProgressStages.findIndex((stage) => stage.statuses.includes(normalizedStatus));
    return idx === -1 ? 0 : idx;
  }

  getCurrentWorkflowStageLabel(status: string | null | undefined): string {
    return this.workflowProgressStages[this.getCurrentWorkflowStageIndex(status)]?.label ?? 'Intake';
  }

  getWorkflowProgressPercent(status: string | null | undefined): number {
    const stageIndex = this.getCurrentWorkflowStageIndex(status);
    const stage = this.workflowProgressStages[stageIndex];
    if (!stage) {
      return 0;
    }

    const normalizedStatus = status ?? '';
    const subStageIndex = stage.statuses.indexOf(normalizedStatus);
    const safeSubStageIndex = subStageIndex === -1 ? 0 : subStageIndex;
    const subStageProgress = stage.statuses.length > 0 ? (safeSubStageIndex + 1) / stage.statuses.length : 0;
    const totalProgress = (stageIndex + subStageProgress) / this.workflowProgressStages.length;
    return Math.round(totalProgress * 100);
  }

  getStageProgressPercent(status: string | null | undefined, stageIndex: number): number {
    const currentStageIndex = this.getCurrentWorkflowStageIndex(status);
    if (stageIndex < currentStageIndex) {
      return 100;
    }

    if (stageIndex > currentStageIndex) {
      return 0;
    }

    const stage = this.workflowProgressStages[stageIndex];
    if (!stage) {
      return 0;
    }

    const normalizedStatus = status ?? '';
    const subStageIndex = stage.statuses.indexOf(normalizedStatus);
    const safeSubStageIndex = subStageIndex === -1 ? 0 : subStageIndex;
    return Math.round(((safeSubStageIndex + 1) / stage.statuses.length) * 100);
  }

  getSubStageState(status: string | null | undefined, stageIndex: number, subStageIndex: number): 'completed' | 'current' | 'upcoming' {
    const currentStageIndex = this.getCurrentWorkflowStageIndex(status);
    if (stageIndex < currentStageIndex) {
      return 'completed';
    }

    if (stageIndex > currentStageIndex) {
      return 'upcoming';
    }

    const stage = this.workflowProgressStages[stageIndex];
    if (!stage) {
      return 'upcoming';
    }

    const normalizedStatus = status ?? '';
    const currentSubStageIndex = stage.statuses.indexOf(normalizedStatus);
    const safeSubStageIndex = currentSubStageIndex === -1 ? 0 : currentSubStageIndex;
    if (subStageIndex < safeSubStageIndex) {
      return 'completed';
    }

    if (subStageIndex === safeSubStageIndex) {
      return 'current';
    }

    return 'upcoming';
  }

  formatJson(value: string | null | undefined): string {
    if (!value) {
      return '{}';
    }

    try {
      return JSON.stringify(JSON.parse(value), null, 2);
    }
    catch {
      return value;
    }
  }
}
