import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import {
  FormBuilder,
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { Subject, Subscription, debounceTime, finalize, forkJoin } from 'rxjs';
import {
  CreateWorkflowTransitionRequest,
  ToggleWorkflowTransitionRequest,
  UpdateWorkflowTransitionRequest,
  ValidateWorkflowTransitionResponse,
  WorkflowMetaCatalog,
  WorkflowMetaItem,
  WorkflowTransition,
  WorkflowTransitionChangeLog
} from '../../core/models/workflow.models';
import { WorkflowApiService } from '../../core/services/workflow-api.service';
import {
  ChipMultiSelectComponent,
  ChipOption
} from '../../shared/chip-multi-select/chip-multi-select.component';
import { WorkflowGraphComponent } from './workflow-graph.component';

interface TransitionForm {
  code: FormControl<string>;
  phase: FormControl<string>;
  sortOrder: FormControl<number>;
  toStatus: FormControl<string>;
  triggerName: FormControl<string>;
  triggerType: FormControl<string>;
  configSlot: FormControl<string>;
  description: FormControl<string>;
  fromStatuses: FormControl<string[]>;
  requiredRoles: FormControl<string[]>;
  gateChecks: FormControl<string[]>;
  successActions: FormControl<string[]>;
  failureActions: FormControl<string[]>;
  workItemsToCreate: FormControl<string[]>;
  changeReason: FormControl<string>;
}

interface PhaseGroup {
  phase: string;
  rows: WorkflowTransition[];
  enabledCount: number;
  disabledCount: number;
}

@Component({
  selector: 'app-workflow-transitions-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    ChipMultiSelectComponent,
    WorkflowGraphComponent
  ],
  templateUrl: './workflow-transitions.page.html',
  styleUrl: './workflow-transitions.page.css'
})
export class WorkflowTransitionsPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(WorkflowApiService);
  private readonly fb = inject(FormBuilder);

  loading = false;
  saving = false;
  pageError = '';
  pageMessage = '';
  validation: ValidateWorkflowTransitionResponse | null = null;

  meta: WorkflowMetaCatalog | null = null;
  transitions: WorkflowTransition[] = [];
  filterEnabled: 'all' | 'enabled' | 'disabled' = 'all';
  sortMode: 'workflow' | 'code' | 'updated' = 'workflow';
  search = '';
  collapsedPhases = new Set<string>();

  selected: WorkflowTransition | null = null;
  selectedStaleHash = false;
  isCreating = false;
  isDirty = false;
  showGraph = true;
  changeLog: WorkflowTransitionChangeLog[] = [];

  private readonly validate$ = new Subject<void>();
  private subs: Subscription[] = [];

  readonly form: FormGroup<TransitionForm> = this.fb.nonNullable.group({
    code: this.fb.nonNullable.control('', Validators.required),
    phase: this.fb.nonNullable.control('Other', Validators.required),
    sortOrder: this.fb.nonNullable.control(0, [Validators.required, Validators.min(0)]),
    toStatus: this.fb.nonNullable.control('', Validators.required),
    triggerName: this.fb.nonNullable.control('', Validators.required),
    triggerType: this.fb.nonNullable.control('User', Validators.required),
    configSlot: this.fb.nonNullable.control(''),
    description: this.fb.nonNullable.control(''),
    fromStatuses: this.fb.nonNullable.control<string[]>([], (c) =>
      Array.isArray(c.value) && (c.value as string[]).length > 0 ? null : { required: true }
    ),
    requiredRoles: this.fb.nonNullable.control<string[]>([]),
    gateChecks: this.fb.nonNullable.control<string[]>([]),
    successActions: this.fb.nonNullable.control<string[]>(['Audit']),
    failureActions: this.fb.nonNullable.control<string[]>([]),
    workItemsToCreate: this.fb.nonNullable.control<string[]>([]),
    changeReason: this.fb.nonNullable.control('')
  });

  ngOnInit(): void {
    this.reload();

    this.subs.push(
      this.form.valueChanges.subscribe(() => {
        if (this.form.dirty) {
          this.isDirty = true;
          this.validate$.next();
        }
      })
    );

    this.subs.push(
      this.validate$.pipe(debounceTime(300)).subscribe(() => this.runLiveValidation())
    );
  }

  ngOnDestroy(): void {
    this.subs.forEach((s) => s.unsubscribe());
  }

  // ── Loading ──────────────────────────────────────────────────────────────
  reload(preserveSelectionId?: string): void {
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

          const keepId = preserveSelectionId ?? this.selected?.id;
          if (keepId) {
            const fresh = transitions.find((t) => t.id === keepId) ?? null;
            this.selectedStaleHash =
              !!fresh && !!this.selected && fresh.concurrencyHash !== this.selected.concurrencyHash;
            this.selected = fresh;
            if (fresh && !this.isDirty) this.loadIntoForm(fresh);
          }
        },
        error: (err: HttpErrorResponse) => {
          this.pageError = this.errorText(err);
        }
      });
  }

  // ── Filtering / grouping ─────────────────────────────────────────────────
  get filteredTransitions(): WorkflowTransition[] {
    const q = this.search.trim().toLowerCase();
    return this.transitions.filter((t) => {
      if (this.filterEnabled === 'enabled' && !t.isEnabled) return false;
      if (this.filterEnabled === 'disabled' && t.isEnabled) return false;
      if (q.length === 0) return true;
      return (
        t.code.toLowerCase().includes(q) ||
        t.triggerName.toLowerCase().includes(q) ||
        (t.description ?? '').toLowerCase().includes(q) ||
        t.toStatus.toLowerCase().includes(q) ||
        t.fromStatuses.some((s) => s.toLowerCase().includes(q)) ||
        t.requiredRoles.some((r) => r.toLowerCase().includes(q))
      );
    });
  }

  get phaseGroups(): PhaseGroup[] {
    const statusIndex = this.statusOrderIndex;
    const triggerWeight = (t: string) => (t === 'User' ? 0 : t === 'System' ? 1 : 2);
    const minStatusIdx = (statuses: string[]) => {
      let m = Number.MAX_SAFE_INTEGER;
      for (const s of statuses) {
        const i = statusIndex.get(s);
        if (i !== undefined && i < m) m = i;
      }
      return m;
    };
    const updatedTs = (t: WorkflowTransition) =>
      Date.parse(t.updatedAt ?? t.createdAt) || 0;

    const groups = new Map<string, PhaseGroup>();
    const phaseRank = new Map<string, number>();
    for (const t of this.filteredTransitions) {
      let g = groups.get(t.phase);
      if (!g) {
        g = { phase: t.phase, rows: [], enabledCount: 0, disabledCount: 0 };
        groups.set(t.phase, g);
      }
      g.rows.push(t);
      if (t.isEnabled) g.enabledCount++;
      else g.disabledCount++;

      // Phase rank = smallest status index touched by any of its transitions.
      const rank = Math.min(minStatusIdx(t.fromStatuses), statusIndex.get(t.toStatus) ?? Number.MAX_SAFE_INTEGER);
      const cur = phaseRank.get(t.phase);
      if (cur === undefined || rank < cur) phaseRank.set(t.phase, rank);
    }

    const compareRows = (a: WorkflowTransition, b: WorkflowTransition): number => {
      // Disabled always sinks to the bottom of its group.
      if (a.isEnabled !== b.isEnabled) return a.isEnabled ? -1 : 1;
      switch (this.sortMode) {
        case 'code':
          return a.code.localeCompare(b.code);
        case 'updated':
          return updatedTs(b) - updatedTs(a) || a.code.localeCompare(b.code);
        case 'workflow':
        default:
          return (
            minStatusIdx(a.fromStatuses) - minStatusIdx(b.fromStatuses) ||
            (statusIndex.get(a.toStatus) ?? Number.MAX_SAFE_INTEGER) -
              (statusIndex.get(b.toStatus) ?? Number.MAX_SAFE_INTEGER) ||
            triggerWeight(a.triggerType) - triggerWeight(b.triggerType) ||
            a.sortOrder - b.sortOrder ||
            a.code.localeCompare(b.code)
          );
      }
    };

    for (const g of groups.values()) g.rows.sort(compareRows);

    return Array.from(groups.values()).sort((a, b) => {
      if (this.sortMode === 'workflow') {
        const ra = phaseRank.get(a.phase) ?? Number.MAX_SAFE_INTEGER;
        const rb = phaseRank.get(b.phase) ?? Number.MAX_SAFE_INTEGER;
        if (ra !== rb) return ra - rb;
      }
      return a.phase.localeCompare(b.phase);
    });
  }

  private get statusOrderIndex(): Map<string, number> {
    const map = new Map<string, number>();
    const items = this.meta?.caseStatuses ?? [];
    for (let i = 0; i < items.length; i++) map.set(items[i].code, i);
    return map;
  }

  togglePhase(phase: string): void {
    if (this.collapsedPhases.has(phase)) this.collapsedPhases.delete(phase);
    else this.collapsedPhases.add(phase);
  }

  isPhaseCollapsed(phase: string): boolean {
    return this.collapsedPhases.has(phase);
  }

  phaseColor(phase: string): string {
    let hash = 0;
    for (let i = 0; i < phase.length; i++) hash = (hash * 31 + phase.charCodeAt(i)) | 0;
    const hue = Math.abs(hash) % 360;
    return `hsl(${hue}, 55%, 88%)`;
  }

  triggerIcon(triggerType: string): string {
    switch (triggerType) {
      case 'System':
        return '⚙';
      case 'ExternalEvent':
        return '⇆';
      default:
        return '👤';
    }
  }

  // ── Vocabulary → ChipOption mappers ──────────────────────────────────────
  optsFrom(items?: WorkflowMetaItem[] | null): ChipOption[] {
    return (items ?? []).map((i) => ({ code: i.code, description: i.description }));
  }

  get workItemOptions(): ChipOption[] {
    const wi = (this.meta?.workItemTypes ?? []).map((i) => ({
      code: i.code,
      description: i.description,
      group: 'Work item'
    }));
    const cf = (this.meta?.caseFormTypes ?? []).map((i) => ({
      code: i.code,
      description: i.description,
      group: 'Form'
    }));
    return [...wi, ...cf];
  }

  get knownPhases(): string[] {
    return Array.from(new Set(this.transitions.map((t) => t.phase))).sort();
  }

  get knownTriggerNames(): string[] {
    return Array.from(new Set(this.transitions.map((t) => t.triggerName))).sort();
  }

  // ── Editor open/close ────────────────────────────────────────────────────
  startCreate(prefill?: WorkflowTransition): void {
    if (!this.confirmDiscardIfDirty()) return;
    this.isCreating = true;
    this.selected = null;
    this.validation = null;
    this.changeLog = [];
    this.pageMessage = '';
    this.pageError = '';
    this.form.reset({
      code: prefill ? `${prefill.code}-COPY` : '',
      phase: prefill?.phase ?? 'Other',
      sortOrder: prefill ? prefill.sortOrder + 1 : 0,
      toStatus: prefill?.toStatus ?? this.meta?.caseStatuses[0]?.code ?? '',
      triggerName: prefill?.triggerName ?? '',
      triggerType: prefill?.triggerType ?? 'User',
      configSlot: prefill?.configSlot ?? '',
      description: prefill?.description ?? '',
      fromStatuses: prefill ? [...prefill.fromStatuses] : [],
      requiredRoles: prefill ? [...prefill.requiredRoles] : [],
      gateChecks: prefill ? [...prefill.gateChecks] : [],
      successActions: prefill ? [...prefill.successActions] : ['Audit'],
      failureActions: prefill ? [...prefill.failureActions] : [],
      workItemsToCreate: prefill ? [...prefill.workItemsToCreate] : [],
      changeReason: ''
    });
    this.form.controls.code.enable();
    this.form.markAsDirty();
    this.isDirty = true;
    this.validate$.next();
  }

  duplicate(t: WorkflowTransition, ev?: Event): void {
    ev?.stopPropagation();
    this.startCreate(t);
  }

  select(t: WorkflowTransition): void {
    if (this.selected?.id === t.id) return;
    if (!this.confirmDiscardIfDirty()) return;
    this.isCreating = false;
    this.selected = t;
    this.selectedStaleHash = false;
    this.validation = null;
    this.pageMessage = '';
    this.pageError = '';
    this.loadIntoForm(t);
    this.api.getWorkflowTransitionChangeLog(t.id, 50).subscribe({
      next: (rows) => (this.changeLog = rows),
      error: () => (this.changeLog = [])
    });
  }

  closeEditor(): void {
    if (!this.confirmDiscardIfDirty()) return;
    this.isCreating = false;
    this.selected = null;
    this.isDirty = false;
    this.validation = null;
  }

  private confirmDiscardIfDirty(): boolean {
    if (!this.isDirty) return true;
    return confirm('Discard unsaved changes?');
  }

  // ── Validation / save / toggle / delete ──────────────────────────────────
  private runLiveValidation(): void {
    if (this.form.invalid) {
      this.validation = null;
      return;
    }
    const req = this.toCreateRequest();
    this.api.validateWorkflowTransition(req).subscribe({
      next: (res) => (this.validation = res),
      error: () => (this.validation = null)
    });
  }

  validateNow(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.runLiveValidation();
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
            this.isDirty = false;
            this.reload(created.id);
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
            this.isDirty = false;
            this.reload(updated.id);
          },
          error: (err: HttpErrorResponse) => this.handleMutationError(err)
        });
    }
  }

  toggleEnabled(target?: WorkflowTransition, ev?: Event): void {
    ev?.stopPropagation();
    const t = target ?? this.selected;
    if (!t) return;
    const reason = target ? null : this.form.controls.changeReason.value || null;
    const req: ToggleWorkflowTransitionRequest = {
      expectedHash: t.concurrencyHash,
      changeReason: reason
    };
    const obs = t.isEnabled
      ? this.api.disableWorkflowTransition(t.id, req)
      : this.api.enableWorkflowTransition(t.id, req);
    this.saving = true;
    obs.pipe(finalize(() => (this.saving = false))).subscribe({
      next: (updated) => {
        this.pageMessage = `${updated.isEnabled ? 'Enabled' : 'Disabled'} ${updated.code}.`;
        if (this.selected?.id === updated.id) this.selected = updated;
        this.reload(updated.id);
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
          this.isDirty = false;
          this.reload();
        },
        error: (err: HttpErrorResponse) => this.handleMutationError(err)
      });
  }

  reloadLatest(): void {
    if (!this.selected) return;
    const id = this.selected.id;
    this.api.getWorkflowTransitions().subscribe({
      next: (rows) => {
        this.transitions = rows;
        const fresh = rows.find((r) => r.id === id) ?? null;
        this.selected = fresh;
        this.selectedStaleHash = false;
        this.isDirty = false;
        if (fresh) this.loadIntoForm(fresh);
      }
    });
  }

  // ── Sort-order helpers ───────────────────────────────────────────────────
  bumpSort(delta: number): void {
    const c = this.form.controls.sortOrder;
    c.setValue(Math.max(0, (c.value ?? 0) + delta));
    c.markAsDirty();
    this.isDirty = true;
  }

  // ── "Related transitions" (Phase D1) ─────────────────────────────────────
  get relatedTransitions(): WorkflowTransition[] {
    if (!this.selected) return [];
    const sel = this.selected;
    return this.transitions.filter(
      (t) =>
        t.id !== sel.id &&
        (t.toStatus === sel.toStatus ||
          t.fromStatuses.some((f) => sel.fromStatuses.includes(f)))
    );
  }

  // ── Form ↔ request mapping ───────────────────────────────────────────────
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
      fromStatuses: [...t.fromStatuses],
      requiredRoles: [...t.requiredRoles],
      gateChecks: [...t.gateChecks],
      successActions: [...t.successActions],
      failureActions: [...t.failureActions],
      workItemsToCreate: [...t.workItemsToCreate],
      changeReason: ''
    });
    this.form.controls.code.disable();
    this.isDirty = false;
    this.validation = null;
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
      fromStatuses: v.fromStatuses,
      requiredRoles: v.requiredRoles,
      gateChecks: v.gateChecks,
      successActions: v.successActions,
      failureActions: v.failureActions,
      workItemsToCreate: v.workItemsToCreate,
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
      this.pageError = 'Validation failed — see details below.';
    } else if (err.status === 409) {
      this.selectedStaleHash = true;
      this.pageError =
        (err.error?.message ?? 'Transition was modified by someone else.') +
        ' Click "Reload latest" to see the current version.';
    } else {
      this.pageError = this.errorText(err);
    }
  }

  private errorText(err: HttpErrorResponse): string {
    return err?.error?.message ?? err?.error?.title ?? err.message ?? 'Request failed.';
  }
}

function emptyToNull(s: string | null | undefined): string | null {
  if (!s) return null;
  const v = s.trim();
  return v.length === 0 ? null : v;
}
