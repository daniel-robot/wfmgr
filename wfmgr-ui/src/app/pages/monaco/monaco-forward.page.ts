import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { WorkflowApiService } from '../../core/services/workflow-api.service';

@Component({
  selector: 'app-monaco-forward-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './monaco-forward.page.html',
  styleUrl: './monaco-forward.page.css'
})
export class MonacoForwardPageComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(WorkflowApiService);
  private static readonly guidPattern = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

  readonly form = this.fb.group({
    caseId: ['', [Validators.required, Validators.pattern(MonacoForwardPageComponent.guidPattern)]]
  });

  busy = false;
  error = '';
  ok = '';

  submit(): void {
    const rawCaseId = (this.form.value.caseId ?? '').trim();
    const normalizedCaseId = rawCaseId.startsWith('{') && rawCaseId.endsWith('}')
      ? rawCaseId.slice(1, -1).trim()
      : rawCaseId;

    this.form.patchValue({ caseId: normalizedCaseId }, { emitEvent: false });

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.error = 'CaseId must be a valid GUID, for example 123e4567-e89b-12d3-a456-426614174000.';
      return;
    }

    this.busy = true;
    this.error = '';
    this.ok = '';

    this.api
      .forwardToMonaco(normalizedCaseId)
      .pipe(finalize(() => (this.busy = false)))
      .subscribe({
        next: () => (this.ok = 'Forward command submitted.'),
        error: (err) =>
          (this.error =
            err?.error?.errors?.caseId?.[0]
            ?? err?.error?.message
            ?? err?.error?.title
            ?? 'Failed to forward case to Monaco.')
      });
  }
}
