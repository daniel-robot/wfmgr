import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  CreateWorkflowVocabularyTermRequest,
  ToggleWorkflowVocabularyTermRequest,
  UpdateWorkflowVocabularyTermRequest,
  WorkflowVocabularyChangeLog,
  WorkflowVocabularyKind,
  WorkflowVocabularyTerm
} from '../../core/models/workflow.models';
import { WorkflowApiService } from '../../core/services/workflow-api.service';

interface CreateForm {
  kind: FormControl<WorkflowVocabularyKind>;
  code: FormControl<string>;
  displayName: FormControl<string>;
  description: FormControl<string>;
  changeReason: FormControl<string>;
}

interface EditForm {
  displayName: FormControl<string>;
  description: FormControl<string>;
  sortOrder: FormControl<number>;
  changeReason: FormControl<string>;
}

const KIND_LABELS: Record<WorkflowVocabularyKind, string> = {
  Role: 'Roles',
  WorkItemType: 'Work item types',
  CaseFormType: 'Case form types',
};

const KINDS: WorkflowVocabularyKind[] = ['Role', 'WorkItemType', 'CaseFormType'];

@Component({
  selector: 'app-workflow-vocabulary-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './workflow-vocabulary.page.html',
  styleUrl: './workflow-vocabulary.page.css'
})
export class WorkflowVocabularyPageComponent implements OnInit {
  private readonly api = inject(WorkflowApiService);
  private readonly fb = inject(FormBuilder);

  readonly kinds = KINDS;
  readonly kindLabels = KIND_LABELS;

  loading = false;
  saving = false;
  pageError = '';
  pageMessage = '';

  activeKind: WorkflowVocabularyKind = 'Role';
  terms: WorkflowVocabularyTerm[] = [];
  selected: WorkflowVocabularyTerm | null = null;
  changeLog: WorkflowVocabularyChangeLog[] = [];

  readonly createForm: FormGroup<CreateForm> = this.fb.nonNullable.group({
    kind: this.fb.nonNullable.control<WorkflowVocabularyKind>('Role', Validators.required),
    code: this.fb.nonNullable.control('', [Validators.required, Validators.pattern(/^[A-Za-z][A-Za-z0-9_-]{0,127}$/)]),
    displayName: this.fb.nonNullable.control(''),
    description: this.fb.nonNullable.control(''),
    changeReason: this.fb.nonNullable.control('')
  });

  readonly editForm: FormGroup<EditForm> = this.fb.nonNullable.group({
    displayName: this.fb.nonNullable.control(''),
    description: this.fb.nonNullable.control(''),
    sortOrder: this.fb.nonNullable.control(0, [Validators.min(0)]),
    changeReason: this.fb.nonNullable.control('')
  });

  ngOnInit(): void {
    this.reload();
  }

  switchKind(kind: WorkflowVocabularyKind): void {
    this.activeKind = kind;
    this.createForm.controls.kind.setValue(kind);
    this.selected = null;
    this.changeLog = [];
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.pageError = '';
    this.api.getWorkflowVocabulary(this.activeKind)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (rows) => {
          this.terms = rows;
          if (this.selected) {
            const fresh = rows.find((r) => r.id === this.selected!.id);
            this.selected = fresh ?? null;
            if (fresh) this.loadEditForm(fresh);
          }
        },
        error: (err: HttpErrorResponse) => (this.pageError = this.errorText(err))
      });
  }

  select(term: WorkflowVocabularyTerm): void {
    this.selected = term;
    this.pageMessage = '';
    this.loadEditForm(term);
    this.api.getWorkflowVocabularyChangeLog(term.id, 50).subscribe({
      next: (rows) => (this.changeLog = rows),
      error: () => (this.changeLog = [])
    });
  }

  create(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }
    const v = this.createForm.getRawValue();
    const req: CreateWorkflowVocabularyTermRequest = {
      kind: v.kind,
      code: v.code,
      displayName: emptyToNull(v.displayName),
      description: emptyToNull(v.description),
      sortOrder: null,
      changeReason: emptyToNull(v.changeReason)
    };
    this.saving = true;
    this.api.createWorkflowVocabularyTerm(req)
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: (term) => {
          this.pageMessage = `Created ${term.kind} '${term.code}'.`;
          this.createForm.reset({ kind: this.activeKind, code: '', displayName: '', description: '', changeReason: '' });
          this.activeKind = term.kind;
          this.selected = term;
          this.reload();
        },
        error: (err: HttpErrorResponse) => (this.pageError = this.errorText(err))
      });
  }

  saveEdit(): void {
    if (!this.selected || this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      return;
    }
    const v = this.editForm.getRawValue();
    const req: UpdateWorkflowVocabularyTermRequest = {
      displayName: emptyToNull(v.displayName),
      description: emptyToNull(v.description),
      sortOrder: v.sortOrder,
      expectedHash: this.selected.concurrencyHash,
      changeReason: emptyToNull(v.changeReason)
    };
    this.saving = true;
    this.api.updateWorkflowVocabularyTerm(this.selected.id, req)
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: (term) => {
          this.pageMessage = `Saved '${term.code}'.`;
          this.selected = term;
          this.reload();
        },
        error: (err: HttpErrorResponse) => (this.pageError = this.errorText(err))
      });
  }

  toggleEnabled(): void {
    if (!this.selected) return;
    const target = this.selected;
    const reason = this.editForm.controls.changeReason.value || null;
    const req: ToggleWorkflowVocabularyTermRequest = {
      expectedHash: target.concurrencyHash,
      changeReason: reason
    };
    const obs = target.isEnabled
      ? this.api.disableWorkflowVocabularyTerm(target.id, req)
      : this.api.enableWorkflowVocabularyTerm(target.id, req);
    this.saving = true;
    obs.pipe(finalize(() => (this.saving = false))).subscribe({
      next: (term) => {
        this.pageMessage = `${term.isEnabled ? 'Enabled' : 'Disabled'} '${term.code}'.`;
        this.selected = term;
        this.reload();
      },
      error: (err: HttpErrorResponse) => (this.pageError = this.errorText(err))
    });
  }

  deleteSelected(): void {
    if (!this.selected) return;
    if (this.selected.isSystem) {
      this.pageError = 'System-seeded terms cannot be deleted; disable them instead.';
      return;
    }
    const target = this.selected;
    if (!confirm(`Delete vocabulary term '${target.code}'?`)) return;
    this.saving = true;
    this.api.deleteWorkflowVocabularyTerm(target.id, target.concurrencyHash, this.editForm.controls.changeReason.value || null)
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          this.pageMessage = `Deleted '${target.code}'.`;
          this.selected = null;
          this.reload();
        },
        error: (err: HttpErrorResponse) => (this.pageError = this.errorText(err))
      });
  }

  private loadEditForm(term: WorkflowVocabularyTerm): void {
    this.editForm.reset({
      displayName: term.displayName ?? '',
      description: term.description ?? '',
      sortOrder: term.sortOrder,
      changeReason: ''
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
