import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { Patient } from '../../core/models/workflow.models';
import { WorkflowApiService } from '../../core/services/workflow-api.service';

@Component({
  selector: 'app-patient-list-page',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './patient-list.page.html',
  styleUrl: './patient-list.page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PatientListPageComponent implements OnInit {
  private readonly api = inject(WorkflowApiService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly cdr = inject(ChangeDetectorRef);

  patients: Patient[] = [];
  loading = false;
  error = '';

  showCreateForm = false;
  creating = false;
  createError = '';

  /** patientId of the row currently showing the start-workflow form */
  activeWorkflowPatientId: string | null = null;
  startingWorkflow = false;
  workflowError = '';

  readonly createForm = this.fb.group({
    hospitalId: ['', Validators.required],
    siteId: ['', Validators.required],
    departmentId: ['', Validators.required],
    externalPatientId: ['', Validators.required],
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    dateOfBirth: ['', Validators.required]
  });

  readonly workflowForm = this.fb.group({
    accessionNumber: ['', Validators.required],
    notes: ['']
  });

  ngOnInit(): void {
    this.loadPatients();
  }

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

  openWorkflowForm(patientId: string): void {
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

  fullName(p: Patient): string {
    return `${p.firstName} ${p.lastName}`;
  }
}
