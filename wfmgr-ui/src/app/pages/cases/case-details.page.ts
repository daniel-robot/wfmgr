import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { forkJoin, finalize } from 'rxjs';
import {
  AuditLogItem,
  CaseDetails,
  CtImageStoredRequest,
  PvMedEventRequest,
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
  workItems: WorkItem[] = [];
  auditLogs: AuditLogItem[] = [];
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

    forkJoin({
      details: this.api.getCaseById(this.caseId),
      workItems: this.api.getCaseWorkItems(this.caseId),
      auditLogs: this.api.getCaseAuditLogs(this.caseId)
    })
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: ({ details, workItems, auditLogs }) => {
          this.details = details;
          this.workItems = workItems;
          this.auditLogs = auditLogs;
          this.infoMessage = `Refreshed at ${new Date().toLocaleTimeString()}.`;
          if (!this.ctEventForm.value.accessionNumber) {
            this.ctEventForm.patchValue({ accessionNumber: details.accessionNumber });
          }
        },
        error: (err) => (this.error = err?.error?.message ?? 'Failed to load case details.')
      });
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
    if (this.ctEventForm.invalid) {
      this.ctEventForm.markAllAsTouched();
      return;
    }

    const req: CtImageStoredRequest = {
      externalEventId: this.ctEventForm.value.externalEventId!,
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
