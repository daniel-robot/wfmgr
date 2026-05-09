export interface CreateCaseRequest {
  hospitalId: string;
  siteId: string;
  departmentId: string;
  accessionNumber: string;
  patientId?: string | null;
  notes?: string | null;
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

export interface TransitionHistoryItem {
  transitionId: string;
  caseId: string;
  fromStatus: string | null;
  toStatus: string;
  triggerType: string;
  triggerName: string;
  triggeredBy: string | null;
  reason: string | null;
  metadataJson: string | null;
  createdAt: string;
}

export interface CaseFormItem {
  formId: string;
  caseId: string;
  formType: string;
  formVersion: number;
  status: string;
  payloadJson: string;
  submittedBy: string | null;
  submittedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CaseAttachmentItem {
  attachmentId: string;
  caseId: string;
  category: string;
  fileName: string;
  storagePath: string;
  sourceSystem: string | null;
  uploadedBy: string | null;
  uploadedAt: string;
}

export interface ExternalEventItem {
  eventId: string;
  caseId: string;
  source: string;
  type: string;
  externalId: string | null;
  caseCorrelationKey: string | null;
  processStatus: string;
  error: string | null;
  receivedAt: string;
  processedAt: string | null;
  payloadJson: string;
}

export interface IntegrationReferenceItem {
  id: string;
  caseId: string;
  systemName: string;
  externalEntityType: string;
  externalId: string;
  externalStatus: string | null;
  metadataJson: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface PlanVersionItem {
  planVersionId: string;
  caseId: string;
  versionNo: number;
  sourceSystem: string | null;
  status: string;
  summaryJson: string | null;
  createdAt: string;
}

export interface WorkflowOption {
  value: string;
  label: string;
}

export interface CreateCaseFormDraftRequest {
  formType: string;
  payloadJson: string;
  formVersion?: number | null;
}

export interface SubmitCaseFormRequest {
  payloadJson?: string | null;
  submittedBy: string;
  reason?: string | null;
}

export interface WorkflowActionRequest {
  triggeredBy: string;
  reason?: string | null;
}

export interface Patient {
  patientId: string;
  hospitalId: string;
  siteId: string;
  departmentId: string;
  externalPatientId: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreatePatientRequest {
  hospitalId: string;
  siteId: string;
  departmentId: string;
  externalPatientId: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
}

export interface StartWorkflowRequest {
  accessionNumber: string;
  notes?: string | null;
}

export interface ApiError {
  message: string;
}

export interface WorkflowProfile {
  id: string;
  key: string;
  name: string | null;
  version: number;
  hospitalId: string | null;
  siteId: string | null;
  departmentId: string | null;
  isActive: boolean;
  concurrencyHash: string;
  createdAt: string | null;
  updatedAt: string | null;
}

export interface WorkflowRule {
  id: string;
  profileId: string;
  slotCode: string;
  priority: number;
  enabled: boolean;
  concurrencyHash: string;
  conditionJson: string | null;
  configJson: string;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  createdAt: string | null;
  updatedAt: string | null;
}

export interface WorkflowProfileDetail {
  profile: WorkflowProfile;
  rules: WorkflowRule[];
}

export interface CreateWorkflowProfileRequest {
  name: string;
  version: number;
  hospitalId?: string | null;
  siteId?: string | null;
  departmentId?: string | null;
  isActive: boolean;
  changeReason?: string | null;
}

export interface UpdateWorkflowProfileRequest {
  name?: string;
  version?: number;
  hospitalId?: string | null;
  siteId?: string | null;
  departmentId?: string | null;
  isActive?: boolean;
  expectedHash?: string | null;
  changeReason?: string | null;
}

export interface ToggleWorkflowProfileRequest {
  expectedHash?: string | null;
  changeReason?: string | null;
}

export interface CreateWorkflowRuleRequest {
  slotCode: string;
  priority: number;
  enabled: boolean;
  conditionJson?: string | null;
  configJson: string;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  changeReason?: string | null;
}

export interface UpdateWorkflowRuleRequest {
  slotCode: string;
  priority: number;
  enabled: boolean;
  conditionJson?: string | null;
  configJson: string;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  expectedHash?: string | null;
  changeReason?: string | null;
}

export interface ToggleWorkflowRuleRequest {
  expectedHash?: string | null;
  changeReason?: string | null;
}

export interface WorkflowMutationConflict {
  message: string;
  currentHash: string | null;
}

export interface ValidateWorkflowRuleRequest {
  slotCode: string;
  configJson: string;
  conditionJson?: string | null;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  priority?: number | null;
}

export interface ValidateWorkflowRuleResponse {
  isValid: boolean;
  errors: string[];
  warnings: string[];
}

export interface WorkflowSlotCode {
  code: string;
  name: string;
  description?: string | null;
}

export interface EffectiveWorkflowSlot {
  slotCode: string;
  sourceProfileId: string | null;
  sourceProfileKey: string | null;
  ruleId: string | null;
  priority: number | null;
  enabled: boolean | null;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  configJson: string | null;
  resolutionReason: string;
}

export interface EffectiveWorkflowQuery {
  hospitalId: string | null;
  siteId: string | null;
  departmentId: string | null;
}

export interface EffectiveWorkflowMatchedProfile {
  id: string;
  key: string;
  version: number;
  hospitalId: string | null;
  siteId: string | null;
  departmentId: string | null;
}

export interface EffectiveWorkflowUnmatchedSlot {
  slotCode: string;
  reason: string;
}

export interface EffectiveWorkflowEvaluatedProfile {
  profileId: string;
  key: string;
  version: number;
  hospitalId: string | null;
  siteId: string | null;
  departmentId: string | null;
  isActive: boolean;
  matchedScope: boolean;
  reasonIncludedOrSkipped: string;
}

export interface EffectiveWorkflowConfig {
  query: EffectiveWorkflowQuery;
  matchedProfile: EffectiveWorkflowMatchedProfile | null;
  resolvedSlots: EffectiveWorkflowSlot[];
  unmatchedSlots: EffectiveWorkflowUnmatchedSlot[];
  evaluatedProfiles: EffectiveWorkflowEvaluatedProfile[];
}
