import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AuditLogItem,
  CaseAttachmentItem,
  CaseDetails,
  CaseFormItem,
  CaseSummary,
  CreateCaseFormDraftRequest,
  CreateCaseRequest,
  CreatePatientRequest,
  CtImageStoredRequest,
  ExternalEventItem,
  IntegrationReferenceItem,
  Patient,
  PlanVersionItem,
  PvMedEventRequest,
  StartWorkflowRequest,
  SubmitCaseFormRequest,
  SubmitSimRecordRequest,
  TransitionHistoryItem,
  WorkflowActionRequest,
  WorkflowOption,
  WorkItem
} from '../models/workflow.models';

@Injectable({ providedIn: 'root' })
export class WorkflowApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  getCases(): Observable<CaseSummary[]> {
    return this.http.get<unknown>(`${this.baseUrl}/api/cases`).pipe(
      map((response) => this.extractArray(response).map((item) => this.toCaseSummary(item)))
    );
  }

  getCaseById(caseId: string): Observable<CaseDetails> {
    return this.http.get<CaseDetails>(`${this.baseUrl}/api/cases/${caseId}`);
  }

  createCase(request: CreateCaseRequest): Observable<{ caseId: string }> {
    return this.http.post<{ caseId: string }>(`${this.baseUrl}/api/cases`, request);
  }

  submitSimRecord(caseId: string, request: SubmitSimRecordRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/api/cases/${caseId}/sim-record`, request);
  }

  forwardToMonaco(caseId: string): Observable<void> {
    const normalizedCaseId = this.normalizeCaseId(caseId);
    return this.http.post<void>(`${this.baseUrl}/api/cases/${normalizedCaseId}/forward/monaco`, {});
  }

  simulateCtImageStored(request: CtImageStoredRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/api/integration/ct/image-stored`, request);
  }

  simulatePvMedEvent(request: PvMedEventRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/api/integration/pvmed/events`, request);
  }

  getCaseWorkItems(caseId: string): Observable<WorkItem[]> {
    return this.http.get<WorkItem[]>(`${this.baseUrl}/api/cases/${caseId}/work-items`);
  }

  getCaseAuditLogs(caseId: string): Observable<AuditLogItem[]> {
    return this.http.get<AuditLogItem[]>(`${this.baseUrl}/api/cases/${caseId}/audit-logs`);
  }

  getCaseTransitionHistory(caseId: string): Observable<TransitionHistoryItem[]> {
    return this.http.get<TransitionHistoryItem[]>(`${this.baseUrl}/api/cases/${caseId}/transition-history`);
  }

  getCaseForms(caseId: string): Observable<CaseFormItem[]> {
    return this.http.get<CaseFormItem[]>(`${this.baseUrl}/api/cases/${caseId}/forms`);
  }

  getCaseAttachments(caseId: string): Observable<CaseAttachmentItem[]> {
    return this.http.get<CaseAttachmentItem[]>(`${this.baseUrl}/api/cases/${caseId}/attachments`);
  }

  getCaseExternalEvents(caseId: string): Observable<ExternalEventItem[]> {
    return this.http.get<ExternalEventItem[]>(`${this.baseUrl}/api/cases/${caseId}/external-events`);
  }

  getCaseIntegrationReferences(caseId: string): Observable<IntegrationReferenceItem[]> {
    return this.http.get<IntegrationReferenceItem[]>(`${this.baseUrl}/api/cases/${caseId}/integration-references`);
  }

  getCasePlanVersions(caseId: string): Observable<PlanVersionItem[]> {
    return this.http.get<PlanVersionItem[]>(`${this.baseUrl}/api/cases/${caseId}/plan-versions`);
  }

  getWorkflowStatuses(): Observable<WorkflowOption[]> {
    return this.http.get<WorkflowOption[]>(`${this.baseUrl}/api/workflow/statuses`);
  }

  getWorkflowWorkItemTypes(): Observable<WorkflowOption[]> {
    return this.http.get<WorkflowOption[]>(`${this.baseUrl}/api/workflow/work-item-types`);
  }

  createCaseFormDraft(caseId: string, request: CreateCaseFormDraftRequest): Observable<CaseFormItem> {
    return this.http.post<CaseFormItem>(`${this.baseUrl}/api/cases/${caseId}/forms/draft`, request);
  }

  submitCaseForm(caseId: string, formId: string, request: SubmitCaseFormRequest): Observable<CaseFormItem> {
    return this.http.post<CaseFormItem>(`${this.baseUrl}/api/cases/${caseId}/forms/${formId}/submit`, request);
  }

  getLatestCaseFormByType(caseId: string, formType: string): Observable<CaseFormItem> {
    return this.http.get<CaseFormItem>(`${this.baseUrl}/api/cases/${caseId}/forms/latest/${encodeURIComponent(formType)}`);
  }

  restartContouring(caseId: string, request: WorkflowActionRequest): Observable<void> {
    return this.postCaseAction(caseId, 'restart-contouring', request);
  }

  rejectContourReview(caseId: string, request: WorkflowActionRequest): Observable<void> {
    return this.postCaseAction(caseId, 'reject-contour-review', request);
  }

  rejectPlanReview(caseId: string, request: WorkflowActionRequest): Observable<void> {
    return this.postCaseAction(caseId, 'reject-plan-review', request);
  }

  rejectPlanReReview(caseId: string, request: WorkflowActionRequest): Observable<void> {
    return this.postCaseAction(caseId, 'reject-plan-rereview', request);
  }

  markPrescriptionSyncFailed(caseId: string, request: WorkflowActionRequest): Observable<void> {
    return this.postCaseAction(caseId, 'prescription-sync-failed', request);
  }

  retryPrescriptionSync(caseId: string, request: WorkflowActionRequest): Observable<void> {
    return this.postCaseAction(caseId, 'retry-prescription-sync', request);
  }

  resolvePrescriptionSync(caseId: string, request: WorkflowActionRequest): Observable<void> {
    return this.postCaseAction(caseId, 'resolve-prescription-sync', request);
  }

  failQa(caseId: string, request: WorkflowActionRequest): Observable<void> {
    return this.postCaseAction(caseId, 'fail-qa', request);
  }

  markSchedulingFailed(caseId: string, request: WorkflowActionRequest): Observable<void> {
    return this.postCaseAction(caseId, 'scheduling-failed', request);
  }

  retryScheduling(caseId: string, request: WorkflowActionRequest): Observable<void> {
    return this.postCaseAction(caseId, 'retry-scheduling', request);
  }

  pauseTreatment(caseId: string, request: WorkflowActionRequest): Observable<void> {
    return this.postCaseAction(caseId, 'pause-treatment', request);
  }

  interruptTreatment(caseId: string, request: WorkflowActionRequest): Observable<void> {
    return this.postCaseAction(caseId, 'interrupt-treatment', request);
  }

  resumeTreatment(caseId: string, request: WorkflowActionRequest): Observable<void> {
    return this.postCaseAction(caseId, 'resume-treatment', request);
  }

  cancelCase(caseId: string, request: WorkflowActionRequest): Observable<void> {
    return this.postCaseAction(caseId, 'cancel', request);
  }

  getAuditLogs(): Observable<AuditLogItem[]> {
    return this.http.get<AuditLogItem[]>(`${this.baseUrl}/api/audit-logs`);
  }

  getPatients(): Observable<Patient[]> {
    return this.http.get<Patient[]>(`${this.baseUrl}/api/patients`);
  }

  getPatientById(patientId: string): Observable<Patient> {
    return this.http.get<Patient>(`${this.baseUrl}/api/patients/${patientId}`);
  }

  createPatient(request: CreatePatientRequest): Observable<Patient> {
    return this.http.post<Patient>(`${this.baseUrl}/api/patients`, request);
  }

  getPatientCases(patientId: string): Observable<CaseSummary[]> {
    return this.http.get<unknown>(`${this.baseUrl}/api/patients/${patientId}/cases`).pipe(
      map((response) => this.extractArray(response).map((item) => this.toCaseSummary(item)))
    );
  }

  startWorkflow(patientId: string, request: StartWorkflowRequest): Observable<{ caseId: string }> {
    return this.http.post<{ caseId: string }>(`${this.baseUrl}/api/patients/${patientId}/cases`, request);
  }

  private postCaseAction(caseId: string, action: string, request: WorkflowActionRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/api/cases/${caseId}/actions/${action}`, request);
  }

  private normalizeCaseId(caseId: string): string {
    const trimmed = (caseId ?? '').trim();
    if (trimmed.startsWith('{') && trimmed.endsWith('}') && trimmed.length > 2) {
      return trimmed.slice(1, -1).trim();
    }

    return trimmed;
  }

  private extractArray(value: unknown): Record<string, unknown>[] {
    if (Array.isArray(value)) {
      return value.filter((x): x is Record<string, unknown> => this.isRecord(x));
    }

    if (!this.isRecord(value)) {
      return [];
    }

    const wrappedItems = value['items'];
    if (Array.isArray(wrappedItems)) {
      return wrappedItems.filter((x): x is Record<string, unknown> => this.isRecord(x));
    }

    const values = value['$values'];
    if (Array.isArray(values)) {
      return values.filter((x): x is Record<string, unknown> => this.isRecord(x));
    }

    return [];
  }

  private toCaseSummary(item: Record<string, unknown>): CaseSummary {
    return {
      caseId: this.readString(item, 'caseId', 'CaseId'),
      hospitalId: this.readString(item, 'hospitalId', 'HospitalId'),
      siteId: this.readString(item, 'siteId', 'SiteId'),
      departmentId: this.readString(item, 'departmentId', 'DepartmentId'),
      accessionNumber: this.readString(item, 'accessionNumber', 'AccessionNumber'),
      patientId: this.readOptionalString(item, 'patientId', 'PatientId'),
      currentStatus: this.readString(item, 'currentStatus', 'CurrentStatus'),
      createdAt: this.readString(item, 'createdAt', 'CreatedAt'),
      updatedAt: this.readOptionalString(item, 'updatedAt', 'UpdatedAt') ?? undefined
    };
  }

  private readString(source: Record<string, unknown>, camelKey: string, pascalKey: string): string {
    const value = source[camelKey] ?? source[pascalKey];
    return typeof value === 'string' ? value : '';
  }

  private readOptionalString(source: Record<string, unknown>, camelKey: string, pascalKey: string): string | null {
    const value = source[camelKey] ?? source[pascalKey];
    if (value === null || value === undefined) {
      return null;
    }

    return typeof value === 'string' ? value : null;
  }

  private isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null;
  }
}
