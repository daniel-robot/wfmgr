import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  EffectiveWorkflowConfig,
  ValidateWorkflowRuleResponse,
  WorkflowProfile,
  WorkflowRule,
  WorkflowSlotCode
} from '../../core/models/workflow.models';
import { WorkflowApiService } from '../../core/services/workflow-api.service';

@Component({
  selector: 'app-workflow-config-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './workflow-config.page.html',
  styleUrl: './workflow-config.page.css'
})
export class WorkflowConfigPageComponent implements OnInit {
  private readonly api = inject(WorkflowApiService);
  private readonly fb = inject(FormBuilder);

  profiles: WorkflowProfile[] = [];
  rules: WorkflowRule[] = [];
  slotCodes: WorkflowSlotCode[] = [];
  selectedProfileId: string | null = null;
  editingRuleId: string | null = null;
  loading = false;
  savingProfile = false;
  savingRule = false;
  validatingRule = false;

  pageError = '';
  pageMessage = '';
  validationResult: ValidateWorkflowRuleResponse | null = null;
  effectivePreview: EffectiveWorkflowConfig | null = null;

  readonly profileForm = this.fb.group({
    name: ['', Validators.required],
    version: [1, [Validators.required, Validators.min(1)]],
    hospitalId: [''],
    siteId: [''],
    departmentId: [''],
    isActive: [true]
  });

  readonly ruleFilterForm = this.fb.group({
    slotCode: [''],
    enabled: ['all']
  });

  readonly ruleForm = this.fb.group({
    slotCode: ['', Validators.required],
    priority: [0, [Validators.required, Validators.min(0)]],
    enabled: [true],
    effectiveFrom: [''],
    effectiveTo: [''],
    conditionJson: [''],
    configJson: ['{}', Validators.required]
  });

  readonly effectiveForm = this.fb.group({
    hospitalId: [''],
    siteId: [''],
    departmentId: ['']
  });

  ngOnInit(): void {
    this.loading = true;
    this.pageError = '';

    this.api.getWorkflowSlotCodes().subscribe({
      next: (items) => {
        this.slotCodes = items;
        if (!this.ruleForm.value.slotCode && items.length > 0) {
          this.ruleForm.patchValue({ slotCode: items[0].code });
        }
      },
      error: (err) => {
        this.pageError = err?.error?.message ?? 'Failed to load slot codes.';
      }
    });

    this.loadProfiles();
  }

  loadProfiles(): void {
    this.loading = true;
    this.api.getWorkflowProfiles()
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({
        next: (items) => {
          this.profiles = items;
          if (items.length === 0) {
            this.selectedProfileId = null;
            this.rules = [];
            this.prepareNewProfile();
            return;
          }

          const currentId = this.selectedProfileId;
          const selected = currentId && items.some((x) => x.id === currentId)
            ? currentId
            : items[0].id;

          this.selectProfile(selected);
        },
        error: (err) => {
          this.pageError = err?.error?.message ?? 'Failed to load workflow profiles.';
        }
      });
  }

  selectProfile(profileId: string): void {
    this.selectedProfileId = profileId;
    this.pageError = '';
    this.pageMessage = '';

    this.api.getWorkflowProfile(profileId).subscribe({
      next: (detail) => {
        this.rules = detail.rules;
        this.profileForm.patchValue({
          name: detail.profile.name ?? '',
          version: detail.profile.version,
          hospitalId: detail.profile.hospitalId ?? '',
          siteId: detail.profile.siteId ?? '',
          departmentId: detail.profile.departmentId ?? '',
          isActive: detail.profile.isActive
        });

        this.editingRuleId = null;
        this.prepareNewRule();
      },
      error: (err) => {
        this.pageError = err?.error?.message ?? 'Failed to load profile details.';
      }
    });
  }

  prepareNewProfile(): void {
    this.selectedProfileId = null;
    this.profileForm.reset({
      name: '',
      version: 1,
      hospitalId: '',
      siteId: '',
      departmentId: '',
      isActive: true
    });
    this.rules = [];
  }

  saveProfile(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.pageError = '';
    this.pageMessage = '';
    this.savingProfile = true;

    const request = {
      name: (this.profileForm.value.name ?? '').trim(),
      version: Number(this.profileForm.value.version ?? 1),
      hospitalId: this.toNullable(this.profileForm.value.hospitalId),
      siteId: this.toNullable(this.profileForm.value.siteId),
      departmentId: this.toNullable(this.profileForm.value.departmentId),
      isActive: !!this.profileForm.value.isActive
    };

    const work = this.selectedProfileId
      ? this.api.updateWorkflowProfile(this.selectedProfileId, request)
      : this.api.createWorkflowProfile(request);

    work.pipe(finalize(() => (this.savingProfile = false))).subscribe({
      next: (profile) => {
        this.pageMessage = this.selectedProfileId
          ? 'Profile updated successfully.'
          : 'Profile created successfully.';
        this.selectedProfileId = profile.id;
        this.loadProfiles();
      },
      error: (err) => {
        this.pageError = this.extractErrors(err) ?? 'Failed to save profile.';
      }
    });
  }

  toggleProfileActive(profile: WorkflowProfile): void {
    this.pageError = '';
    this.pageMessage = '';

    const call = profile.isActive
      ? this.api.deactivateWorkflowProfile(profile.id)
      : this.api.activateWorkflowProfile(profile.id);

    call.subscribe({
      next: () => {
        this.pageMessage = profile.isActive ? 'Profile deactivated.' : 'Profile activated.';
        this.loadProfiles();
      },
      error: (err) => {
        this.pageError = this.extractErrors(err) ?? 'Failed to change profile active state.';
      }
    });
  }

  toggleSelectedProfileActive(): void {
    if (!this.selectedProfileId) {
      return;
    }

    const profile = this.profiles.find((x) => x.id === this.selectedProfileId);
    if (!profile) {
      return;
    }

    this.toggleProfileActive(profile);
  }

  applyRuleFilters(): void {
    if (!this.selectedProfileId) {
      return;
    }

    const slotCode = this.toNullable(this.ruleFilterForm.value.slotCode);
    const enabledRaw = this.ruleFilterForm.value.enabled ?? 'all';
    const enabled = enabledRaw === 'all' ? undefined : enabledRaw === 'true';

    this.api.getWorkflowRules(this.selectedProfileId, { slotCode: slotCode ?? undefined, enabled }).subscribe({
      next: (items) => {
        this.rules = items;
      },
      error: (err) => {
        this.pageError = err?.error?.message ?? 'Failed to load rules.';
      }
    });
  }

  prepareNewRule(): void {
    this.editingRuleId = null;
    this.validationResult = null;
    this.ruleForm.reset({
      slotCode: this.slotCodes[0]?.code ?? '',
      priority: 0,
      enabled: true,
      effectiveFrom: '',
      effectiveTo: '',
      conditionJson: '',
      configJson: '{}'
    });
  }

  editRule(rule: WorkflowRule): void {
    this.editingRuleId = rule.id;
    this.validationResult = null;
    this.ruleForm.patchValue({
      slotCode: rule.slotCode,
      priority: rule.priority,
      enabled: rule.enabled,
      effectiveFrom: this.toLocalDateTimeInput(rule.effectiveFrom),
      effectiveTo: this.toLocalDateTimeInput(rule.effectiveTo),
      conditionJson: this.prettyJson(rule.conditionJson),
      configJson: this.prettyJson(rule.configJson)
    });
  }

  validateRule(): void {
    const clientErrors = this.getClientRuleValidationErrors();
    if (clientErrors.length > 0) {
      this.validationResult = { isValid: false, errors: clientErrors, warnings: [] };
      return;
    }

    this.validatingRule = true;
    this.validationResult = null;

    this.api.validateWorkflowRule(this.buildRuleValidationRequest())
      .pipe(finalize(() => (this.validatingRule = false)))
      .subscribe({
        next: (result) => {
          this.validationResult = result;
        },
        error: (err) => {
          this.validationResult = {
            isValid: false,
            errors: [this.extractErrors(err) ?? 'Validation failed due to server error.'],
            warnings: []
          };
        }
      });
  }

  saveRule(): void {
    if (!this.selectedProfileId) {
      this.pageError = 'Select a profile before creating or updating rules.';
      return;
    }

    const clientErrors = this.getClientRuleValidationErrors();
    if (clientErrors.length > 0) {
      this.validationResult = { isValid: false, errors: clientErrors, warnings: [] };
      return;
    }

    this.savingRule = true;
    this.pageError = '';
    this.pageMessage = '';

    const payload = {
      slotCode: (this.ruleForm.value.slotCode ?? '').trim(),
      priority: Number(this.ruleForm.value.priority ?? 0),
      enabled: !!this.ruleForm.value.enabled,
      conditionJson: this.toNullable(this.ruleForm.value.conditionJson),
      configJson: (this.ruleForm.value.configJson ?? '').trim(),
      effectiveFrom: this.toIso(this.ruleForm.value.effectiveFrom),
      effectiveTo: this.toIso(this.ruleForm.value.effectiveTo),
    };

    const work = this.editingRuleId
      ? this.api.updateWorkflowRule(this.editingRuleId, payload)
      : this.api.createWorkflowRule(this.selectedProfileId, payload);

    work.pipe(finalize(() => (this.savingRule = false))).subscribe({
      next: () => {
        this.pageMessage = this.editingRuleId ? 'Rule updated successfully.' : 'Rule created successfully.';
        this.prepareNewRule();
        this.applyRuleFilters();
      },
      error: (err) => {
        this.pageError = this.extractErrors(err) ?? 'Failed to save rule.';
      }
    });
  }

  toggleRuleEnabled(rule: WorkflowRule): void {
    const work = rule.enabled
      ? this.api.disableWorkflowRule(rule.id)
      : this.api.enableWorkflowRule(rule.id);

    work.subscribe({
      next: () => {
        this.pageMessage = rule.enabled ? 'Rule disabled.' : 'Rule enabled.';
        this.applyRuleFilters();
      },
      error: (err) => {
        this.pageError = this.extractErrors(err) ?? 'Failed to change rule enabled state.';
      }
    });
  }

  loadEffectivePreview(): void {
    this.api.getEffectiveWorkflowConfig({
      hospitalId: this.toNullable(this.effectiveForm.value.hospitalId),
      siteId: this.toNullable(this.effectiveForm.value.siteId),
      departmentId: this.toNullable(this.effectiveForm.value.departmentId)
    }).subscribe({
      next: (result) => {
        this.effectivePreview = result;
      },
      error: (err) => {
        this.pageError = err?.error?.message ?? 'Failed to load effective config preview.';
      }
    });
  }

  formatJsonSummary(value: string | null): string {
    if (!value) {
      return '-';
    }

    try {
      const parsed = JSON.parse(value);
      const compact = JSON.stringify(parsed);
      if (compact.length > 80) {
        return `${compact.slice(0, 80)}...`;
      }

      return compact;
    }
    catch {
      return value.length > 80 ? `${value.slice(0, 80)}...` : value;
    }
  }

  private getClientRuleValidationErrors(): string[] {
    const errors: string[] = [];

    if (!this.ruleForm.value.slotCode || !String(this.ruleForm.value.slotCode).trim()) {
      errors.push('slotCode is required.');
    }

    const priority = Number(this.ruleForm.value.priority ?? 0);
    if (Number.isNaN(priority) || priority < 0) {
      errors.push('priority must be a number greater than or equal to 0.');
    }

    const configJson = (this.ruleForm.value.configJson ?? '').trim();
    if (!configJson) {
      errors.push('configJson is required.');
    } else if (!this.isJson(configJson)) {
      errors.push('configJson must be valid JSON.');
    }

    const conditionJson = this.toNullable(this.ruleForm.value.conditionJson);
    if (conditionJson && !this.isJson(conditionJson)) {
      errors.push('conditionJson must be valid JSON when provided.');
    }

    const from = this.toIso(this.ruleForm.value.effectiveFrom);
    const to = this.toIso(this.ruleForm.value.effectiveTo);
    if (from && to && to < from) {
      errors.push('effectiveTo cannot be earlier than effectiveFrom.');
    }

    return errors;
  }

  private buildRuleValidationRequest() {
    return {
      slotCode: (this.ruleForm.value.slotCode ?? '').trim(),
      configJson: (this.ruleForm.value.configJson ?? '').trim(),
      conditionJson: this.toNullable(this.ruleForm.value.conditionJson),
      effectiveFrom: this.toIso(this.ruleForm.value.effectiveFrom),
      effectiveTo: this.toIso(this.ruleForm.value.effectiveTo),
      priority: Number(this.ruleForm.value.priority ?? 0)
    };
  }

  private toNullable(value: unknown): string | null {
    if (typeof value !== 'string') {
      return null;
    }

    const trimmed = value.trim();
    return trimmed ? trimmed : null;
  }

  private toIso(value: unknown): string | null {
    if (typeof value !== 'string' || !value.trim()) {
      return null;
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return null;
    }

    return date.toISOString();
  }

  private toLocalDateTimeInput(value: string | null): string {
    if (!value) {
      return '';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '';
    }

    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hour = String(date.getHours()).padStart(2, '0');
    const minute = String(date.getMinutes()).padStart(2, '0');
    return `${year}-${month}-${day}T${hour}:${minute}`;
  }

  private prettyJson(value: string | null): string {
    if (!value) {
      return '';
    }

    try {
      return JSON.stringify(JSON.parse(value), null, 2);
    }
    catch {
      return value;
    }
  }

  private isJson(value: string): boolean {
    try {
      JSON.parse(value);
      return true;
    }
    catch {
      return false;
    }
  }

  private extractErrors(err: any): string | null {
    const response = err?.error;
    if (!response) {
      return null;
    }

    if (typeof response.message === 'string') {
      return response.message;
    }

    if (Array.isArray(response.errors)) {
      return response.errors.join(' ');
    }

    if (Array.isArray(response?.errors?.errors)) {
      return response.errors.errors.join(' ');
    }

    if (Array.isArray(response?.errors)) {
      return response.errors.join(' ');
    }

    if (Array.isArray(response?.Errors)) {
      return response.Errors.join(' ');
    }

    if (typeof response.title === 'string') {
      return response.title;
    }

    return null;
  }
}
