import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AuditLogItem,
  CaseDetails,
  CaseSummary,
  CreateCaseRequest,
  CtImageStoredRequest,
  PvMedEventRequest,
  SubmitSimRecordRequest,
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
    return this.http.post<void>(`${this.baseUrl}/api/cases/${caseId}/forward/monaco`, {});
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

  getAuditLogs(): Observable<AuditLogItem[]> {
    return this.http.get<AuditLogItem[]>(`${this.baseUrl}/api/audit-logs`);
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
