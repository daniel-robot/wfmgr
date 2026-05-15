import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  CaseStatusOverlay,
  UpdateCaseStatusOverlayRequest
} from '../../core/models/workflow.models';
import { WorkflowApiService } from '../../core/services/workflow-api.service';

interface OverlayForm {
  displayName: FormControl<string>;
  description: FormControl<string>;
  color: FormControl<string>;
  category: FormControl<string>;
  sortOrder: FormControl<number>;
}

@Component({
  selector: 'app-case-status-overlays-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './case-status-overlays.page.html',
  styleUrl: './case-status-overlays.page.css'
})
export class CaseStatusOverlaysPageComponent implements OnInit {
  private readonly api = inject(WorkflowApiService);
  private readonly fb = inject(FormBuilder);

  loading = false;
  saving = false;
  pageError = '';
  pageMessage = '';

  overlays: CaseStatusOverlay[] = [];
  selected: CaseStatusOverlay | null = null;

  readonly form: FormGroup<OverlayForm> = this.fb.nonNullable.group({
    displayName: this.fb.nonNullable.control(''),
    description: this.fb.nonNullable.control(''),
    color: this.fb.nonNullable.control(''),
    category: this.fb.nonNullable.control(''),
    sortOrder: this.fb.nonNullable.control(0, [Validators.min(0)])
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.pageError = '';
    this.api.getCaseStatusOverlays()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (rows) => {
          this.overlays = rows;
          if (this.selected) {
            const fresh = rows.find((r) => r.code === this.selected!.code);
            this.selected = fresh ?? null;
            if (fresh) this.loadForm(fresh);
          }
        },
        error: (err: HttpErrorResponse) => (this.pageError = this.errorText(err))
      });
  }

  get groupedByCategory(): { category: string; rows: CaseStatusOverlay[] }[] {
    const groups = new Map<string, CaseStatusOverlay[]>();
    for (const o of this.overlays) {
      const key = o.category ?? 'Other';
      const arr = groups.get(key) ?? [];
      arr.push(o);
      groups.set(key, arr);
    }
    return Array.from(groups.entries())
      .map(([category, rows]) => ({
        category,
        rows: rows.slice().sort((a, b) => a.sortOrder - b.sortOrder)
      }))
      .sort((a, b) => a.category.localeCompare(b.category));
  }

  select(o: CaseStatusOverlay): void {
    this.selected = o;
    this.pageMessage = '';
    this.loadForm(o);
  }

  save(): void {
    if (!this.selected || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const req: UpdateCaseStatusOverlayRequest = {
      displayName: emptyToNull(v.displayName),
      description: emptyToNull(v.description),
      color: emptyToNull(v.color),
      category: emptyToNull(v.category),
      sortOrder: v.sortOrder,
      expectedHash: this.selected.concurrencyHash
    };
    this.saving = true;
    this.api.updateCaseStatusOverlay(this.selected.code, req)
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: (overlay) => {
          this.pageMessage = `Saved '${overlay.code}'.`;
          this.selected = overlay;
          this.reload();
        },
        error: (err: HttpErrorResponse) => (this.pageError = this.errorText(err))
      });
  }

  reset(): void {
    if (!this.selected) return;
    if (!confirm(`Reset '${this.selected.code}' to in-code defaults?`)) return;
    this.saving = true;
    this.api.resetCaseStatusOverlay(this.selected.code)
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: (overlay) => {
          this.pageMessage = `Reset '${overlay.code}'.`;
          this.selected = overlay;
          this.reload();
        },
        error: (err: HttpErrorResponse) => (this.pageError = this.errorText(err))
      });
  }

  private loadForm(o: CaseStatusOverlay): void {
    this.form.reset({
      displayName: o.displayName ?? '',
      description: o.description ?? '',
      color: o.color ?? '',
      category: o.category ?? '',
      sortOrder: o.sortOrder
    });
  }

  private errorText(err: HttpErrorResponse): string {
    if (err.status === 400 && err.error?.errors) {
      return (err.error.errors as string[]).join('; ');
    }
    return err?.error?.message ?? err?.error?.title ?? err.message ?? 'Request failed.';
  }
}

function emptyToNull(s: string | null | undefined): string | null {
  if (!s) return null;
  const v = s.trim();
  return v.length === 0 ? null : v;
}
