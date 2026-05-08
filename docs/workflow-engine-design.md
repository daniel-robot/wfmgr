# Workflow Engine Design

This document describes the internals of the `Wfmgr.Application.Workflows.V1` workflow engine used for radiotherapy case lifecycle management.

---

## Table of Contents

1. [Transition Catalog](#1-transition-catalog)
2. [Gate Checks](#2-gate-checks)
3. [Compensation Catalog](#3-compensation-catalog)
4. [External Event Integration](#4-external-event-integration)
5. [Outbox Failure Escalation](#5-outbox-failure-escalation)
6. [Workflow Configuration](#6-workflow-configuration)

---

## 1. Transition Catalog

### Structure

Every allowed state-machine transition in the system is described as a `TransitionDefinition` record:

```csharp
public sealed record TransitionDefinition
{
    string   Code;                    // e.g. "SIM-001"
    CaseStatus[] FromStatuses;        // one or more valid source statuses
    CaseStatus   ToStatus;            // target status on success
    string   TriggerName;             // human-readable trigger key
    WorkflowTriggerType TriggerType;  // User | System | ExternalEvent
    string?  RequiredRole;            // slash-delimited, e.g. "Doctor/Admin"
    string[] GateChecks;              // ordered list of gate check names
    string[] SuccessActions;          // documentary labels (executed by side-effect layer)
    string[] FailureActions;          // documentary labels
    string[] WorkItemsToCreate;       // work-item types to open on success
    string?  ConfigSlot;              // optional behaviour policy slot
}
```

All definitions live in `WorkflowTransitionCatalog` as `static readonly` fields. Two collection properties are derived once at startup:

| Property | Type | Use |
|----------|------|-----|
| `All` | `IReadOnlyList<TransitionDefinition>` | iteration / docs / validation |
| `ByCode` | `IReadOnlyDictionary<string, TransitionDefinition>` | O(1) lookup by `Code` |

### Phases and codes

| Phase | Code range | Transitions |
|-------|-----------|-------------|
| 1 – Intake & Simulation | SIM-001 – SIM-005 | 5 |
| 2 – Image Acquisition | IMG-001 – IMG-003 | 3 |
| 3 – Contouring | CON-001 – CON-005 | 5 |
| 4 – Contour Review | REV-001 – REV-004 | 4 |
| 5 – Treatment Planning | PLN-001 – PLN-006 | 6 |
| 6 – Re-review & Prescription | RX-001 – RX-007 | 7 |
| 7 – Plan QA & Double-check | QA-001 – QA-008 | 8 |
| 8 – Scheduling, Order & Treatment | TRT-001 – TRT-012 | 12 |
| 9 – Post-treatment & Archiving | POST-001 – POST-003 | 3 |
| **Total** | | **53** |

### Execution pipeline

`CaseTransitionService.ApplyTransitionAsync` runs the following steps on every call:

```
1. Catalog lookup  — find TransitionDefinition by (triggerName, fromStatus)
2. Role check      — RequiredRole slash-list matched against GateValidationContext.Roles
3. Gate validation — IGateValidationService.ValidateAsync for all declared GateChecks
4. Status mutation — CaseData.CurrentStatus = ToStatus; StatusVersion++
5. Audit log       — AuditLog row written via IWorkflowDataAccess
6. Transition history — CaseTransitionHistory row written
7. Side effects    — IWorkflowSideEffectService.ExecuteAsync (catalog-matched only)
```

When no catalog entry matches and `fallbackToStatus` is provided, steps 2–3 and step 7 are skipped. This preserves backward compatibility for legacy trigger names that have not yet been migrated to the catalog.

---

## 2. Gate Checks

### Purpose

Gate checks are precondition validators that must all pass before a transition proceeds. They are named string constants (see `GateCheckNames`) and evaluated by `GateValidationService`.

### Evaluation semantics

- All declared checks are evaluated — the service does **not** short-circuit on the first failure. This means callers receive a complete list of blocking conditions.
- A check returns `null` when it passes, or a human-readable failure string when it does not.
- An unknown check name (not in the strategy map) produces a failure with the message `"not implemented"`, preventing silent pass-through.
- The aggregate result is `GateValidationResult`, which carries `IsSuccess`, a `Summary` string, and a `Failures` list.

### Strategy map

`GateValidationService` initialises a `Dictionary<string, GateCheck>` in its constructor. Each entry maps a `GateCheckNames` constant to an async delegate. Aliases (different constant names sharing the same implementation) are explicitly registered as separate map entries:

```
SimulationRequestFormValid → SimulationRequestFormValidAsync
SimulationRecordFormValid  → SimulationRecordFormValidAsync
SimulationScheduleExists   → SimulationScheduleExistsAsync
CaseNotCancelled           → CaseNotCancelledAsync
CaseActiveNotCancelled     → CaseNotCancelledAsync        ← alias
TreatmentNotStarted        → CancellationAllowedAsync
CancellationAllowed        → CancellationAllowedAsync
ImageReferenceExists       → ImageReferenceExistsAsync
ImageRefsValid             → ImageReferenceExistsAsync    ← alias
ImageAccessible            → ImageReferenceExistsAsync    ← alias
CaseResolvedByCorrelationKey → ExternalPayloadPresentAsync
ExternalAcceptOrDeliveryConfirmed → ExternalPayloadPresentAsync
ContourResultExists        → ContourResultExistsAsync
ContourResultRefsValid     → ContourResultExistsAsync     ← alias
RevisedContourExists       → ContourResultExistsAsync     ← alias
EventValid                 → ExternalPayloadPresentAsync
EventIdempotent            → EventIdempotentAsync
ManualContourPayloadValid  → FormOrPayloadPresentAsync
RetryAllowed               → RetryAllowedAsync
ReviewApprovalExists       → ReviewApprovalExistsAsync
MinimumApprovalsReached    → ReviewApprovalExistsAsync    ← alias
RejectionReasonRequired    → ReasonPresentAsync
PlanVersionExists          → PlanVersionExistsAsync
PlanPayloadValid           → FormOrPayloadPresentAsync
AssigneeExists             → AssigneeExistsAsync
TaskAssigned               → WorkItemIdPresentAsync
PlanEvaluationApproved / EvaluationApproved → PlanEvaluationApprovedAsync
ReasonRequired / FailureReasonRequired / ReworkDecisionMade → ReasonPresentAsync
S4ReReviewEnabled          → S4ReReviewEnabledAsync
NoReReviewRequired         → S4ReReviewDisabledAsync
ReReviewApproved           → ReReviewApprovedAsync
PrescriptionReferenceValid → PrescriptionReferenceExistsAsync
FailureEventValid          → ExternalPayloadPresentAsync
PlanAndPrescriptionPresent → PlanAndPrescriptionPresentAsync
QAFormValid / QAFormApproved → QAApprovalExistsAsync
DoubleCheckApproved        → DoubleCheckApprovedAsync
S5DoubleCheckEnabled       → S5DoubleCheckEnabledAsync
S5DoubleCheckDisabled      → S5DoubleCheckDisabledAsync
ScheduleReferenceExists / ScheduleExists / CaseReleasedForSchedule → ScheduleReferenceExistsAsync
SchedulePayloadValid       → ExternalPayloadPresentAsync
TreatmentOrderFormValid    → TreatmentOrderFormValidAsync
QueueOrAppointmentValid    → ExternalPayloadPresentAsync
TreatmentStartEventValid   → ExternalPayloadPresentAsync
FractionDataValid          → ExternalPayloadPresentAsync
S7CompletionRuleSatisfied  → S7CompletionRuleSatisfiedAsync
NoBlockingTasks            → NoBlockingTasksAsync
RequiredFormsComplete      → RequiredFormsCompleteAsync
PostTreatmentReviewFormValid → PostTreatmentReviewFormValidAsync
TreatmentCompleted         → TreatmentCompletedAsync
PauseReasonProvided        → ReasonPresentAsync
InterruptionReasonRequired → ReasonPresentAsync
MedicalApprovalExists      → MedicalApprovalExistsAsync
ResumeAllowed              → ReasonPresentAsync
```

### Policy-slot gate checks

Several gate checks (`S4ReReviewEnabled`, `S5DoubleCheckEnabled`, …) consult the `IWorkflowProfileResolver` to read per-department policy slots at runtime. The profile resolver is called lazily — at most once per slot key per `ValidateAsync` call — using nullable local variables to cache the result within the call.

---

## 3. Compensation Catalog

### Structure

Each `CompensationDefinition` record describes how to recover from a named failure:

```csharp
public sealed record CompensationDefinition
{
    string      Code;                    // e.g. "CMP-001"
    string      FailedStepCode;          // transition code that can fail
    string      FailureCondition;        // human description
    string      CompensationAction;      // human description
    CaseStatus? TargetStatus;            // status to restore (null = keep current)
    string?     WorkItemToCreate;        // work-item type to open (null = none)
    bool        ManualInterventionRequired;
    RetryPolicy? RetryPolicy;            // null | ExponentialBackoff | LimitedRetry | TimerEscalation
}
```

The catalog is `WorkflowCompensationCatalog`. Two derived collections are available:

| Property | Key |
|----------|-----|
| `All` | ordered list |
| `ByCode` | `Code` → `CompensationDefinition` |
| `ByFailedStep` | `FailedStepCode` → `IReadOnlyList<CompensationDefinition>` |

### Compensation rules (CMP-001 – CMP-020)

| Code | Failed step | Condition | Recovery | Target status |
|------|-------------|-----------|----------|---------------|
| CMP-001 | IMG-002 | Outbox send to contouring tool failed | Retry with exponential back-off; manual-forward work item after limit | `ImageForwarding` |
| CMP-002 | IMG-003 | Contouring tool not accepting images | Keep at `ImageStored`; allow manual resend | `ImageStored` |
| CMP-003 | CON-002 | Auto-contour result invalid or corrupt | Request manual contouring | `ContourReworkRequired` |
| CMP-004 | CON-003 | PvMed / third-party auto-contour system failed | Create manual-contouring work item | `ContourReworkRequired` |
| CMP-005 | REV-003 | Contour review rejected by clinician | Route back for contour rework | `ContoursRejected` |
| CMP-006 | PLN-005 | Plan evaluation failed during review | Reopen planning; new plan-design task | `PlanningInProgress` |
| CMP-007 | RX-004 | Plan re-review rejected | Reopen planning with new plan version | `PlanningInProgress` |
| CMP-008 | RX-006 | Prescription sync to oncology system failed | Manual sync work item; auto-retry if within policy | `PrescriptionSyncFailed` |
| CMP-009 | QA-003 | Physics QA failed | Return to planning; new plan-design task | `PlanQAFailed` |
| CMP-010 | QA-008 | Independent double-check failed | Reopen planning path | `PlanningInProgress` |
| CMP-011 | TRT-001 | MSQ schedule sync timed out | Retry; manual scheduling task after limit | `SchedulingInProgress` |
| CMP-012 | TRT-004 | Treatment order failed validation | Keep at `OrderPending`; require operator to correct | `OrderPending` |
| CMP-013 | TRT-005 | Queue integration failed | Keep at `OrderSubmitted`; local/manual queue fallback | `OrderSubmitted` |
| CMP-014 | TRT-008 | Paused case not resumed within SLA | Escalation work item; notify clinician | `TreatmentPaused` |
| CMP-015 | TRT-010 | Treatment interrupted | Exception work item; full manual clinical resolution | `TreatmentInterrupted` |
| CMP-016 | TRT-012 | Completion data incomplete | Keep in `Treating`; continue monitoring | `Treating` |
| CMP-017 | POST-002 | Post-treatment review form incomplete | Keep at `PostTreatmentReviewPending`; require operator | `PostTreatmentReviewPending` |
| CMP-018 | POST-003 | Archive blocked by open task | Reject archive; keep at `PostTreatmentReviewed` | `PostTreatmentReviewed` |
| CMP-019 | SIM-005 | Cancellation not medically permitted | Reject cancellation; status unchanged | (none) |
| CMP-020 | ANY_EXTERNAL_EVENT | Duplicate external event | Ignore safely; no status or work-item change | (none) |

### Execution pipeline

`WorkflowCompensationService.HandleFailureAsync`:

```
1. Resolve definition   — WorkflowCompensationCatalog.ByFailedStep lookup
2. Load case            — IWorkflowDataAccess.GetCaseByIdAsync
3. Status change        — delegate to ICaseTransitionService with synthetic trigger
                          "Compensate:<Code>", skipping role + gate checks
4. Work item            — IWorkItemLifecycleService creates work item if defined
5. Retry outbox         — if RetryPolicy is set, enqueue a retry outbox message
6. Return result        — CompensationResult.Success(code, fromStatus, toStatus,
                          workItemCreated, retryDispatched)
```

`SaveChangesAsync` is **not** called by the compensation service — the caller owns the unit of work.

---

## 4. External Event Integration

### Flow

External systems (PvMed contouring, Monaco TPS, MSQ radiotherapy management) push events to `POST /api/externalevents`. The `ExternalEventsController` routes each event to `ExternalEventDispatcher.DispatchAsync`, which is implemented in `Wfmgr.Infrastructure`.

```
ExternalEventsController
    └─ ExternalEventDispatcher.DispatchAsync(request, ct)
           ├─ resolve caseData by accession number / correlation key
           ├─ guard: idempotency check (duplicate event → ignore)
           ├─ route by ExternalIntegrationEventTypes constant
           │      ├─ happy-path: call IStateMachineService.ApplyTransitionAsync
           │      │              or IWorkflowDataAccess.EnqueueOutboxAsync
           │      └─ failure event: call IWorkflowCompensationService.HandleFailureAsync
           └─ SaveChangesAsync (unit-of-work boundary)
```

### Idempotency guard

Before processing, the dispatcher checks whether an outbox or audit record with the same external event ID already exists. If so, the event is safely discarded with no state change (matching CMP-020 semantics).

### Compensation routing for failure events

The following failure events trigger direct compensation rather than a state-machine transition:

| Event type | Failed step code | Compensation rule |
|------------|-----------------|-------------------|
| `AutoContourFailed` | `CON-003` | CMP-004 |
| `PrescriptionSyncFailed` | `RX-006` | CMP-008 |
| `TreatmentInterrupted` | `TRT-010` | CMP-015 |

In each case `HandleFailureAsync` is called inline, within the same database transaction scope as the event receipt.

---

## 5. Outbox Failure Escalation

### Outbox pattern

Outbound calls to external systems (sending images to the contouring tool, prescriptions to MSQ, schedule syncs) are never made synchronously from application services. Instead, `IWorkflowDataAccess.EnqueueOutboxAsync` writes an `OutboxMessage` row inside the same transaction that changes the case status. This guarantees at-least-once delivery even if the process restarts mid-operation.

### OutboxWorker processing loop

`OutboxWorker` (a hosted `BackgroundService` in `Wfmgr.Api`) polls for pending outbox messages on a timer, attempts delivery, and updates the message status:

```
Pending → (attempt delivery)
    ├─ success  → Sent      (NextRetryAt = null)
    └─ failure  → RetryCount += 1
                  ├─ RetryCount < 5  → Retrying   (NextRetryAt = now + backoff)
                  └─ RetryCount ≥ 5  → Failed     (route to compensation)
```

`CompensationRetryThreshold = 5` is a compile-time constant in `OutboxWorker`.

### Action → compensation routing table

When the retry budget is exhausted the worker maps `OutboxMessage.Action` to a `failedStepCode` and calls `IWorkflowCompensationService.HandleFailureAsync`:

| `OutboxActions` constant | Failed step code | Compensation rule |
|--------------------------|-----------------|-------------------|
| `SendImagesToContourTool` | `IMG-002` | CMP-001 |
| `SendToMonacoImport` | `IMG-002` | CMP-001 |
| `SyncSchedule` | `TRT-001` | CMP-011 |
| `GeneratePrescription` | `RX-006` | CMP-008 |

Any other action with an exhausted retry budget is marked `Failed` without escalation.

### Unit-of-work boundary

Both the outbox status update (`Failed`) and the compensation side-effects (status change, work item creation) are persisted in a single `SaveChangesAsync` call inside the worker so that neither can be committed without the other.

---

## 6. Workflow Configuration

### Purpose

The workflow engine now includes a configuration layer for policy slots **S1-S8**. These slots do **not** replace the transition catalog or compensation catalog. Instead, they parameterize behavior inside existing workflow phases such as contouring, review, plan dispatch, cancellation handling, and completion checks.

The runtime engine remains catalog-driven:

- `WorkflowTransitionCatalog` still decides which transitions are legal.
- `WorkflowCompensationCatalog` still decides how failures are recovered.
- Slot configuration influences policy decisions within those transitions and side effects.

### Slot model

Each configurable policy is represented by a stable slot code constant in `WorkflowSlotCodes`:

| Slot | Code | Purpose |
|------|------|---------|
| S1 | `S1_CONTOURING_STRATEGY` | Auto contour provider and fallback behavior |
| S2 | `S2_CONTOUR_REVIEW_POLICY` | Review mode, rejection behavior, timeout |
| S3 | `S3_PLAN_DISPATCH` | Planning assignment mode and escalation |
| S4 | `S4_PLAN_REREVIEW_POLICY` | Optional re-review trigger and reviewer role |
| S5 | `S5_PLAN_DOUBLE_CHECK` | Optional independent double-check policy |
| S6 | `S6_QUEUE_AND_CANCEL_POLICY` | Queue mode and cancellation constraints |
| S7 | `S7_TREATMENT_COMPLETION_POLICY` | Completion mode and mismatch handling |
| S8 | `S8_EXCEPTION_HANDLING_POLICY` | Retry strategy, manual fallback, notifications |

The backing persistence model remains:

- `WorkflowProfileEntity` — an active/inactive scope definition for hospital / site / department
- `WorkflowRuleEntity` — a per-slot rule with priority, enable flag, effective window, and JSON config

### Runtime resolution

`WorkflowProfileResolver` is the runtime entry point used by the engine and side-effect layer. For each slot it performs the following steps:

```
1. Match the most specific active profile by hospital/site/department.
2. Within that profile, select the highest-priority rule for the slot.
3. Ignore rules that are disabled or outside the effective window.
4. Deserialize the slot config JSON into the typed slot model.
5. Validate the typed config with WorkflowSlotConfigValidator.
6. If no valid rule remains, return built-in fallback defaults.
```

Profile matching follows a four-level fallback chain:

1. exact `hospitalId + siteId + departmentId`
2. exact `hospitalId + siteId`, `departmentId = null`
3. exact `hospitalId`, `siteId = null`, `departmentId = null`
4. global default (`hospitalId = null`, `siteId = null`, `departmentId = null`)

Important semantics:

- A **disabled** rule is ignored by the resolver.
- Disabling a rule does **not** remove a workflow phase.
- If no enabled/effective rule is found, the engine falls back to the slot's default typed configuration.

For example, disabling an S1 rule does not skip contouring. It removes that policy override, causing runtime to fall back to the default `S1ContouringStrategy` values.

### Engine integration points

Slots are consumed from several parts of the engine:

- `CaseWorkflowService` reads S1 during CT image storage, contour completion, and manual Monaco forward decisions.
- `GateValidationService` consults slot-driven gates such as `S4ReReviewEnabled`, `S5DoubleCheckEnabled`, and `S7CompletionRuleSatisfied`.
- `WorkflowSideEffectService` resolves S1-S5 lazily to determine assigned roles and external dispatch targets.

Representative effects by slot:

- **S1** changes contour provider, auto-contour behavior, and auto-forward / manual-forward behavior.
- **S2** changes contour review expectations and rework routing.
- **S3** changes planning target role and dispatch behavior.
- **S4** toggles whether plan re-review logic is enabled.
- **S5** toggles whether double-check logic is enabled.
- **S6** shapes queue mode and cancellation behavior.
- **S7** shapes treatment completion checks.
- **S8** shapes retry/backoff and exception fallback behavior.

See also the generated overview diagram:

- `docs/workflow/generated/workflow-slots-overview.puml`

### Configuration management API

The API surface for slot administration is exposed by `WorkflowConfigController` under `/api/workflow-config`.

Implemented endpoints include:

| Method | Route | Purpose |
|--------|-------|---------|
| `GET` | `/api/workflow-config/profiles` | List workflow profiles |
| `GET` | `/api/workflow-config/profiles/{profileId}` | Get profile detail with rules |
| `POST` | `/api/workflow-config/profiles` | Create profile |
| `PUT` | `/api/workflow-config/profiles/{profileId}` | Update profile |
| `POST` | `/api/workflow-config/profiles/{profileId}/activate` | Activate profile |
| `POST` | `/api/workflow-config/profiles/{profileId}/deactivate` | Deactivate profile |
| `GET` | `/api/workflow-config/profiles/{profileId}/rules` | List rules for a profile |
| `POST` | `/api/workflow-config/profiles/{profileId}/rules` | Create rule |
| `GET` | `/api/workflow-config/rules/{ruleId}` | Get rule |
| `PUT` | `/api/workflow-config/rules/{ruleId}` | Update rule |
| `POST` | `/api/workflow-config/rules/{ruleId}/enable` | Enable rule |
| `POST` | `/api/workflow-config/rules/{ruleId}/disable` | Disable rule |
| `POST` | `/api/workflow-config/rules/validate` | Validate slot rule JSON/config |
| `GET` | `/api/workflow-config/slot-codes` | List supported slot codes |
| `GET` | `/api/workflow-config/effective` | Preview effective resolved configuration |

### Validation and conflict handling

`WorkflowConfigService.ValidateRuleAsync` performs server-side validation before create/update:

- slot code is required and must be one of S1-S8
- priority must be `>= 0`
- `effectiveTo >= effectiveFrom`
- `configJson` must be valid JSON
- `conditionJson`, when present, must be valid JSON
- slot-specific typed validation is delegated to `WorkflowSlotConfigValidator`

`conditionJson` is currently stored but **not interpreted** by the runtime resolver. The validation endpoint returns this as a warning rather than an error.

Optimistic concurrency is implemented with a lightweight hash token:

- `WorkflowProfileDto` and `WorkflowRuleDto` expose `ConcurrencyHash`
- update / enable / disable requests accept `ExpectedHash`
- controller endpoints return `409 Conflict` when the current hash differs from the submitted hash

The hash is computed from editable fields only, not from EF-specific concurrency primitives.

### Effective configuration preview

`GET /api/workflow-config/effective` returns an explainable preview rather than only the winning JSON blobs. The response includes:

- the input query (`hospitalId`, `siteId`, `departmentId`)
- the matched profile, if any
- resolved slots with source profile, rule id, priority, effective window, config JSON, and resolution reason
- unmatched slots with a textual explanation
- evaluated profiles showing which scope candidates were included or skipped during fallback resolution

This preview does not alter runtime behavior; it is a read-only explainability surface for administrators.

### Security and audit status

Workflow configuration is currently an administrative feature without live RBAC enforcement. The codebase exposes a policy constant:

- `WorkflowConfigPolicies.Admin = "WorkflowConfigAdmin"`

but the API remains unprotected in local development. The current code intentionally leaves a TODO instead of introducing a fake or incomplete auth system.

Likewise, workflow configuration mutation auditing is **deferred**. Existing audit persistence is case-centric and is not reused for configuration changes. The current implementation explicitly leaves a TODO to wire configuration changes into a future non-case-scoped audit pipeline.

### Test coverage

Focused integration tests now live in `Wfmgr.Api.Tests/WorkflowConfigApiTests.cs`. Covered scenarios include:

- slot code enumeration endpoint
- validation failure for unsupported slot codes
- validation failure for invalid JSON config
- create / update rule happy path
- `409 Conflict` on stale concurrency hash
- effective preview explainability payload

These tests use `WebApplicationFactory<Program>` with EF Core InMemory storage and are intended to validate the workflow configuration slice without changing workflow runtime semantics.
