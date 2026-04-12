import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { CaseSummary, Patient } from '../../core/models/workflow.models';
import { WorkflowApiService } from '../../core/services/workflow-api.service';

type WorkflowStepState = 'completed' | 'current' | 'upcoming';

interface WorkflowStep {
  id: string;
  label: string;
}

@Component({
  selector: 'app-patient-list-page',
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './patient-list.page.html',
  styleUrl: './patient-list.page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PatientListPageComponent implements OnInit {
  private readonly api = inject(WorkflowApiService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly cdr = inject(ChangeDetectorRef);

  // ── Patients ──────────────────────────────────────────────────────────────
  patients: Patient[] = [];
  loading = false;
  error = '';

  // ── Create patient form ───────────────────────────────────────────────────
  showCreateForm = false;
  creating = false;
  createError = '';

  readonly createForm = this.fb.group({
    hospitalId: ['', Validators.required],
    siteId: ['', Validators.required],
    departmentId: ['', Validators.required],
    externalPatientId: ['', Validators.required],
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    dateOfBirth: ['', Validators.required]
  });

  // ── Expanded patient / lazy-loaded cases ──────────────────────────────────
  expandedPatientId: string | null = null;
  /** Cache: patientId → cases */
  patientCasesMap = new Map<string, CaseSummary[]>();
  loadingCasesFor: string | null = null;

  // ── Start workflow form ───────────────────────────────────────────────────
  activeWorkflowPatientId: string | null = null;
  startingWorkflow = false;
  workflowError = '';

  readonly workflowForm = this.fb.group({
    accessionNumber: ['', Validators.required],
    notes: ['']
  });

  // ── Workflow step visualisation (mirrors case-list) ───────────────────────
  readonly workflowSteps: WorkflowStep[] = [
    { id: 'intake',      label: 'Intake' },
    { id: 'simulation',  label: 'Simulation' },
    { id: 'contouring',  label: 'Contouring' },
    { id: 'planning',    label: 'Planning' },
    { id: 'treatment',   label: 'Treatment' }
  ];

  private readonly statusToStep = new Map<string, number>([
    // Step 0 — Intake
    ['Cancelled', 0],
    // Step 1 — Simulation (sim request submitted through sim completed)
    ['Submitted', 1],
    ['SimScheduled', 1], ['SimInProgress', 1], ['SimCompleted', 1],
    // Step 2 — Contouring (images arrive → contouring complete)
    ['ImageStored', 2], ['ImageForwarding', 2],
    ['ContouringInProgress', 2], ['ContoursReady', 2],
    ['ContoursUnderReview', 2], ['ContoursRejected', 2], ['ContourReworkRequired', 2],
    // Step 3 — Planning
    ['PlanningPending', 3], ['PlanningAssigned', 3], ['PlanningInProgress', 3],
    ['PlanReady', 3], ['PlanUnderReview', 3], ['PlanReviewed', 3],
    ['PlanReReviewOptional', 3], ['PrescriptionGenerating', 3], ['PrescriptionReady', 3],
    ['PrescriptionSyncFailed', 3], ['PlanQAInProgress', 3], ['PlanQAApproved', 3],
    ['PlanQAFailed', 3], ['PlanDoubleCheckOptional', 3],
    // Step 4 — Treatment
    ['ReadyForScheduling', 4], ['SchedulingInProgress', 4], ['SchedulingFailed', 4],
    ['Scheduled', 4], ['OrderPending', 4], ['OrderSubmitted', 4], ['QueuePending', 4],
    ['Treating', 4], ['TreatmentPaused', 4], ['TreatmentInterrupted', 4],
    ['TreatmentCompleted', 4], ['PostTreatmentReviewPending', 4], ['PostTreatmentReviewed', 4],
    ['Archived', 4], ['Completed', 4], ['MonacoForwarded', 4]
  ]);

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.loadPatients();
  }

  // ── Patient actions ───────────────────────────────────────────────────────
  loadPatients(): void {
    this.loading = true;
    this.error = '';

    this.api
      .getPatients()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (data) => {
          this.patients = data;
          this.cdr.markForCheck();
        },
        error: () => {
          this.error = 'Failed to load patients.';
          this.cdr.markForCheck();
        }
      });
  }

  openCreateForm(): void {
    this.showCreateForm = true;
    this.createForm.reset();
    this.createError = '';
    this.cdr.markForCheck();
  }

  cancelCreate(): void {
    this.showCreateForm = false;
    this.createError = '';
    this.cdr.markForCheck();
  }

  submitCreatePatient(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }

    this.creating = true;
    this.createError = '';

    this.api
      .createPatient({
        hospitalId: this.createForm.value.hospitalId!,
        siteId: this.createForm.value.siteId!,
        departmentId: this.createForm.value.departmentId!,
        externalPatientId: this.createForm.value.externalPatientId!,
        firstName: this.createForm.value.firstName!,
        lastName: this.createForm.value.lastName!,
        dateOfBirth: this.createForm.value.dateOfBirth!
      })
      .pipe(finalize(() => (this.creating = false)))
      .subscribe({
        next: (patient) => {
          this.patients = [patient, ...this.patients];
          this.showCreateForm = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.createError = err?.error?.message ?? 'Failed to create patient.';
          this.cdr.markForCheck();
        }
      });
  }

  // ── Expand / collapse patient row  ────────────────────────────────────────
  toggleExpand(patientId: string): void {
    if (this.expandedPatientId === patientId) {
      this.expandedPatientId = null;
      this.activeWorkflowPatientId = null;
      this.cdr.markForCheck();
      return;
    }

    this.expandedPatientId = patientId;
    this.activeWorkflowPatientId = null;
    this.workflowError = '';

    if (!this.patientCasesMap.has(patientId)) {
      this.loadCasesForPatient(patientId);
    } else {
      this.cdr.markForCheck();
    }
  }

  private loadCasesForPatient(patientId: string): void {
    this.loadingCasesFor = patientId;

    this.api
      .getPatientCases(patientId)
      .pipe(finalize(() => {
        this.loadingCasesFor = null;
        this.cdr.markForCheck();
      }))
      .subscribe({
        next: (cases) => {
          this.patientCasesMap.set(patientId, cases);
          this.cdr.markForCheck();
        },
        error: () => {
          this.patientCasesMap.set(patientId, []);
          this.cdr.markForCheck();
        }
      });
  }

  casesFor(patientId: string): CaseSummary[] {
    return this.patientCasesMap.get(patientId) ?? [];
  }

  // ── Start workflow ────────────────────────────────────────────────────────
  openWorkflowForm(patientId: string, event: Event): void {
    event.stopPropagation();
    if (this.expandedPatientId !== patientId) {
      this.toggleExpand(patientId);
    }
    this.activeWorkflowPatientId = patientId;
    this.workflowForm.reset();
    this.workflowError = '';
    this.cdr.markForCheck();
  }

  cancelWorkflow(): void {
    this.activeWorkflowPatientId = null;
    this.workflowError = '';
    this.cdr.markForCheck();
  }

  startWorkflow(patientId: string): void {
    if (this.workflowForm.invalid) {
      this.workflowForm.markAllAsTouched();
      return;
    }

    this.startingWorkflow = true;
    this.workflowError = '';

    this.api
      .startWorkflow(patientId, {
        accessionNumber: this.workflowForm.value.accessionNumber!,
        notes: this.workflowForm.value.notes || null
      })
      .pipe(finalize(() => (this.startingWorkflow = false)))
      .subscribe({
        next: (res) => {
          // Invalidate cache so the new case appears on next expand
          this.patientCasesMap.delete(patientId);
          this.activeWorkflowPatientId = null;
          this.cdr.markForCheck();
          this.router.navigate(['/cases', res.caseId]);
        },
        error: (err) => {
          this.workflowError = err?.error?.message ?? 'Failed to start workflow.';
          this.cdr.markForCheck();
        }
      });
  }

  // ── Workflow step helpers ─────────────────────────────────────────────────
  getStepState(status: string, stepIndex: number): WorkflowStepState {
    const current = this.statusToStep.get(status) ?? 0;
    if (stepIndex < current) return 'completed';
    if (stepIndex === current) return 'current';
    return 'upcoming';
  }

  getStatusClass(status: string): string {
    return `status-${status.toLowerCase()}`;
  }

  // ── Utilities ─────────────────────────────────────────────────────────────
  fullName(p: Patient): string {
    return `${p.firstName} ${p.lastName}`;
  }
}
