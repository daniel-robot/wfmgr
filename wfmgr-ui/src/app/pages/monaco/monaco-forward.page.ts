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

  readonly form = this.fb.group({
    caseId: ['', Validators.required]
  });

  busy = false;
  error = '';
  ok = '';

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy = true;
    this.error = '';
    this.ok = '';

    this.api
      .forwardToMonaco(this.form.value.caseId!)
      .pipe(finalize(() => (this.busy = false)))
      .subscribe({
        next: () => (this.ok = 'Forward command submitted.'),
        error: (err) => (this.error = err?.error?.message ?? 'Failed to forward case to Monaco.')
      });
  }
}
