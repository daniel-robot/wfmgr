import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import {
  CreateWorkflowTransitionRequest,
  ToggleWorkflowTransitionRequest,
  UpdateWorkflowTransitionRequest,
  ValidateWorkflowTransitionResponse,
  WorkflowMetaCatalog,
  WorkflowTransition,
  WorkflowTransitionChangeLog
} from '../../core/models/workflow.models';
import { WorkflowApiService } from '../../core/services/workflow-api.service';

interface TransitionForm {
  code: FormControl<string>;
  phase: FormControl<string>;
  sortOrder: FormControl<number>;
  toStatus: FormControl<string>;
  triggerName: FormControl<string>;
  triggerType: FormControl<string>;
  configSlot: FormControl<string>;
  description: FormControl<string>;
  fromStatuses: FormControl<string>; // CSV
  requiredRoles: FormControl<string>; // CSV
  gateChecks: FormControl<string>; // CSV
  successActions: FormControl<string>; // CSV
  failureActions: FormControl<string>; // CSV
  workItemsToCreate: FormControl<string>; // CSV
  changeReason: FormControl<string>;
}

@Component({
  selector: 'app-workflow-transitions-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './workflow-transitions.page.html',
  styleUrl: './workflow-transitions.page.css'
})
export class WorkflowTransitionsPageComponent implements OnInit {
  private readonly api = inject(WorkflowApiService);
  private readonly fb = inject(FormBuilder);

  loading = false;
  saving = false;
  pageError = '';
  pageMessage = '';
  validation: ValidateWorkflowTransitionResponse | null = null;

  meta: WorkflowMetaCatalog | null = null;
  transitions: WorkflowTransition[] = [];
  filterPhase = '';
  filterEnabled: 'all' | 'enabled' | 'disabled' = 'all';

  selected: WorkflowTransition | null = null;
  isCreating = false;
  changeLog: WorkflowTransitionChangeLog[] = [];

  readonly form: FormGroup<TransitionForm> = this.fb.nonNullable.group({
    code: this.fb.nonNullable.control('', Validators.required),
    phase: this.fb.nonNullable.control('Other', Validators.required),
    sortOrder: this.fb.nonNullable.control(0, [Validators.required, Validators.min(0)]),
    toStatus: this.fb.nonNullable.control('', Validators.required),
    triggerName: this.fb.nonNullable.control('', Validators.required),
    triggerType: this.fb.nonNullable.control('User', Validators.required),
    configSlot: this.fb.nonNullable.control(''),
    description: this.fb.nonNullable.control(''),
    fromStatuses: this.fb.nonNullable.control('', Validators.required),
    requiredRoles: this.fb.nonNullable.control(''),
    gateChecks: this.fb.nonNullable.control(''),
    successActions: this.fb.nonNullable.control(''),
    failureActions: this.fb.nonNullable.control(''),
    workItemsToCreate: this.fb.nonNullable.control(''),
    changeReason: this.fb.nonNullable.control('')
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.pageError = '';
    forkJoin({
      meta: this.api.getWorkflowMeta(),
      transitions: this.api.getWorkflowTransitions()
    })
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: ({ meta, transitions }) => {
          this.meta = meta;
          this.transitions = transitions;
          if (this.selected) {
            const updated = transitions.find((t) => t.id === this.selected!.id);
            this.selected = updated ?? null;
            if (updated) this.loadIntoForm(updated);
          }
        },
        error: (err: HttpErrorResponse) => {
          this.pageError = err?.error?.message ?? err.message ?? 'Failed to load workflow transitions.';
        }
      });
  }

  get filteredTransitions(): WorkflowTransition[] {
    return this.transitions.filter((t) => {
      if (this.filterPhase && t.phase !== this.filterPhase) return false;
      if (this.filterEnabled === 'enabled' && !t.isEnabled) return false;
      if (this.filterEnabled === 'disabled' && t.isEnabled) return false;
      return true;
    });
  }

  get phases(): string[] {
    return Array.from(new Set(this.transitions.map((t) => t.phase))).sort();
  }

  startCreate(): void {
    this.isCreating = true;
    this.selected = null;
    this.validation = null;
    this.changeLog = [];
    this.pageMessage = '';
    this.form.reset({
      code: '',
      phase: 'Other',
      sortOrder: 0,
      toStatus: this.meta?.caseStatuses[0]?.code ?? '',
      triggerName: '',
      triggerType: 'User',
      configSlot: '',
      description: '',
      fromStatuses: '',
      requiredRoles: '',
      gateChecks: '',
      successActions: 'Audit',
      failureActions: '',
      workItemsToCreate: '',
      changeReason: ''
    });
    this.form.controls.code.enable();
  }

  select(t: WorkflowTransition): void {
    this.isCreating = false;
    this.selected = t;
    this.validation = null;
    this.pageMessage = '';
    this.loadIntoForm(t);
    this.api.getWorkflowTransitionChangeLog(t.id, 50).subscribe({
      next: (rows) => (this.changeLog = rows),
      error: () => (this.changeLog = [])
    });
  }

  validateNow(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const req = this.toCreateRequest();
    this.api.validateWorkflowTransition(req).subscribe({
      next: (res) => (this.validation = res),
      error: (err: HttpErrorResponse) => (this.pageError = this.errorText(err))
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving = true;
    this.pageError = '';
    this.pageMessage = '';

    if (this.isCreating) {
      const req = this.toCreateRequest();
      this.api
        .createWorkflowTransition(req)
        .pipe(finalize(() => (this.saving = false)))
        .subscribe({
          next: (created) => {
            this.pageMessage = `Created ${created.code}.`;
            this.isCreating = false;
            this.selected = created;
            this.reload();
          },
          error: (err: HttpErrorResponse) => this.handleMutationError(err)
        });
    } else if (this.selected) {
      const req = this.toUpdateRequest(this.selected.concurrencyHash);
      this.api
        .updateWorkflowTransition(this.selected.id, req)
        .pipe(finalize(() => (this.saving = false)))
        .subscribe({
          next: (updated) => {
            this.pageMessage = `Saved ${updated.code}.`;
            this.selected = updated;
            this.reload();
          },
          error: (err: HttpErrorResponse) => this.handleMutationError(err)
        });
    }
  }

  toggleEnabled(): void {
    if (!this.selected) return;
    const target = this.selected;
    const reason = this.form.controls.changeReason.value || null;
    const req: ToggleWorkflowTransitionRequest = {
      expectedHash: target.concurrencyHash,
      changeReason: reason
    };
    const obs = target.isEnabled
      ? this.api.disableWorkflowTransition(target.id, req)
      : this.api.enableWorkflowTransition(target.id, req);
    this.saving = true;
    obs.pipe(finalize(() => (this.saving = false))).subscribe({
      next: (updated) => {
        this.pageMessage = `${updated.isEnabled ? 'Enabled' : 'Disabled'} ${updated.code}.`;
        this.selected = updated;
        this.reload();
      },
      error: (err: HttpErrorResponse) => this.handleMutationError(err)
    });
  }

  delete(): void {
    if (!this.selected) return;
    if (!confirm(`Delete transition ${this.selected.code}? This is a hard delete from the catalog.`)) return;
    const target = this.selected;
    this.saving = true;
    this.api
      .deleteWorkflowTransition(target.id, target.concurrencyHash, this.form.controls.changeReason.value || null)
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          this.pageMessage = `Deleted ${target.code}.`;
          this.selected = null;
          this.reload();
        },
        error: (err: HttpErrorResponse) => this.handleMutationError(err)
      });
  }

  private loadIntoForm(t: WorkflowTransition): void {
    this.form.reset({
      code: t.code,
      phase: t.phase,
      sortOrder: t.sortOrder,
      toStatus: t.toStatus,
      triggerName: t.triggerName,
      triggerType: t.triggerType,
      configSlot: t.configSlot ?? '',
      description: t.description ?? '',
      fromStatuses: t.fromStatuses.join(', '),
      requiredRoles: t.requiredRoles.join(', '),
      gateChecks: t.gateChecks.join(', '),
      successActions: t.successActions.join(', '),
      failureActions: t.failureActions.join(', '),
      workItemsToCreate: t.workItemsToCreate.join(', '),
      changeReason: ''
    });
    // Code is the immutable business key on the server.
    this.form.controls.code.disable();
  }

  private toCreateRequest(): CreateWorkflowTransitionRequest {
    const v = this.form.getRawValue();
    return {
      code: v.code,
      phase: v.phase,
      sortOrder: v.sortOrder,
      toStatus: v.toStatus,
      triggerName: v.triggerName,
      triggerType: v.triggerType,
      configSlot: emptyToNull(v.configSlot),
      description: emptyToNull(v.description),
      fromStatuses: csv(v.fromStatuses),
      requiredRoles: csv(v.requiredRoles),
      gateChecks: csv(v.gateChecks),
      successActions: csv(v.successActions),
      failureActions: csv(v.failureActions),
      workItemsToCreate: csv(v.workItemsToCreate),
      changeReason: emptyToNull(v.changeReason)
    };
  }

  private toUpdateRequest(expectedHash: string): UpdateWorkflowTransitionRequest {
    const c = this.toCreateRequest();
    return {
      phase: c.phase,
      sortOrder: c.sortOrder,
      toStatus: c.toStatus,
      triggerName: c.triggerName,
      triggerType: c.triggerType,
      configSlot: c.configSlot,
      description: c.description,
      fromStatuses: c.fromStatuses,
      requiredRoles: c.requiredRoles,
      gateChecks: c.gateChecks,
      successActions: c.successActions,
      failureActions: c.failureActions,
      workItemsToCreate: c.workItemsToCreate,
      expectedHash,
      changeReason: c.changeReason
    };
  }

  private handleMutationError(err: HttpErrorResponse): void {
    if (err.status === 400 && err.error?.errors) {
      this.validation = err.error as ValidateWorkflowTransitionResponse;
      this.pageError = 'Validation failed — see details.';
    } else if (err.status === 409) {
      this.pageError = err.error?.message ?? 'Conflict — please reload and try again.';
    } else {
      this.pageError = this.errorText(err);
    }
  }

  private errorText(err: HttpErrorResponse): string {
    return err?.error?.message ?? err?.error?.title ?? err.message ?? 'Request failed.';
  }
}

function csv(s: string): string[] {
  return s
    .split(',')
    .map((x) => x.trim())
    .filter((x) => x.length > 0);
}

function emptyToNull(s: string | null | undefined): string | null {
  if (!s) return null;
  const v = s.trim();
  return v.length === 0 ? null : v;
}
