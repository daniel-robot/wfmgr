export interface CreateCaseRequest {
  hospitalId: string;
  siteId: string;
  departmentId: string;
  accessionNumber: string;
  patientId?: string | null;
  notes?: string | null;
}

export interface SubmitSimRecordRequest {
  ctMachineId: string;
  simulatedAt: string;
  recordFormJson: string;
}

export interface CtImageStoredRequest {
  externalEventId: string;
  accessionNumber: string;
  dicomRef: DicomRef;
  dicomWebLocation: DicomWebLocation;
  occurredAt: string;
}

export interface DicomRef {
  studyInstanceUid: string;
  seriesInstanceUids: string[];
  modality: string;
}

export interface DicomWebLocation {
  wadoRsUrl: string;
  authRef?: string | null;
}

export interface PvMedEventRequest {
  externalEventId: string;
  caseId: string;
  type: string;
  pvMedJob: PvMedJob;
  pvMedResult?: PvMedResult | null;
  occurredAt: string;
}

export interface PvMedJob {
  jobId: string;
  status: string;
  progress?: number | null;
}

export interface PvMedResult {
  rtStructLocation: RtStructLocation;
}

export interface RtStructLocation {
  studyInstanceUid: string;
  seriesInstanceUid: string;
}

export interface CaseSummary {
  caseId: string;
  hospitalId: string;
  siteId: string;
  departmentId: string;
  accessionNumber: string;
  patientId?: string | null;
  currentStatus: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CaseDetails extends CaseSummary {
  ctStudyInstanceUid?: string | null;
  ctWadoRsUrl?: string | null;
  pvMedJobId?: string | null;
  rtStructSeriesInstanceUid?: string | null;
}

export interface WorkItem {
  workItemId: string;
  caseId: string;
  type: string;
  status: string;
  assignedRole: string;
  assignedUserId?: string | null;
  createdAt: string;
}

export interface AuditLogItem {
  auditId: string;
  caseId: string;
  actorType: string;
  actorId?: string | null;
  action: string;
  fromStatus?: string | null;
  toStatus?: string | null;
  snapshotJson: string;
  createdAt: string;
}

export interface ApiError {
  message: string;
}
