import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { WorkflowApiService } from '../../core/services/workflow-api.service';

@Component({
  selector: 'app-create-case-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './create-case.page.html',
  styleUrl: './create-case.page.css'
})
export class CreateCasePageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(WorkflowApiService);
  private readonly router = inject(Router);

  readonly form = this.fb.group({
    hospitalId: ['', Validators.required],
    siteId: ['', Validators.required],
    departmentId: ['', Validators.required],
    accessionNumber: ['', Validators.required],
    patientId: [''],
    notes: ['']
  });

  submitting = false;
  error = '';

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting = true;
    this.error = '';

    this.api
      .createCase({
        hospitalId: this.form.value.hospitalId!,
        siteId: this.form.value.siteId!,
        departmentId: this.form.value.departmentId!,
        accessionNumber: this.form.value.accessionNumber!,
        patientId: this.form.value.patientId || null,
        notes: this.form.value.notes || null
      })
      .pipe(finalize(() => (this.submitting = false)))
      .subscribe({
        next: (response) => this.router.navigate(['/cases', response.caseId]),
        error: (err) => (this.error = err?.error?.message ?? 'Failed to create case.')
      });
  }
}
