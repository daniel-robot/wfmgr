# Workflow Engine Design

This document describes the current workflow engine design in the codebase, centered on:

- `Wfmgr.Application.Workflows.V1.CaseWorkflowService`
- `Wfmgr.Application.Workflows.V1.CaseTransitionService`
- `Wfmgr.Infrastructure.Workflows.WorkflowTransitionCatalogService`
- `Wfmgr.Infrastructure.Integrations.ExternalEventDispatcher`
- `Wfmgr.Api.Workers.OutboxWorker`

It reflects the implementation as of May 2026.

---

## 1. Runtime Building Blocks

### 1.1 Orchestration vs transition engine

- `CaseWorkflowService` orchestrates business operations (create case, complete scan, handle CT/PvMed events, reject review, cancel case, etc).
- `CaseTransitionService` executes a single transition with a strict pipeline:
  1. Catalog lookup by `triggerName + fromStatus`
  2. Required-role check
  3. Gate-check validation (`IGateValidationService`)
  4. Case status mutation (`CurrentStatus`, `StatusVersion`, `UpdatedAt`)
  5. Audit log write
  6. Transition history write
  7. Side-effect execution (`IWorkflowSideEffectService`) for catalog-matched transitions
- Save behavior: transition service does not call `SaveChangesAsync`; caller owns the unit of work.

### 1.2 Catalog source of truth

- The in-code `WorkflowTransitionCatalog` remains the canonical seed set.
- Runtime reads and lookups use `IWorkflowTransitionCatalogService` (DB-backed).
- `WorkflowSeedingHostedService` triggers startup seeding for:
  - profiles/rules
  - transitions
  - vocabulary
  - case-status overlays
- Lazy seed paths still exist as fallback (mainly test harnesses without hosted services).

### 1.3 Persistence and reliability

- Data access uses `WorkflowDataAccess`.
- PostgreSQL via EF Core/Npgsql with retry-on-failure configured for deadlock `40P01`.
- External-event idempotency uses both:
  - `ExternalEventInbox` (inbox-first dedup in dispatcher)
  - `ExternalEvent` table (historical/legacy dedup + event history)

---

## 2. Active Transition Set

The static class contains many historical definitions, but `WorkflowTransitionCatalog.All` currently includes a focused active subset used for seed/runtime defaults.

### 2.1 Active transitions in `WorkflowTransitionCatalog.All`

- Intake & Simulation:
  - `SIM-001`, `SIM-001A`, `SIM-002`, `SIM-003`, `SIM-004`, `SIM-004A`, `SIM-005`
- Image Acquisition:
  - `IMG-001`
- Contouring (granular path):
  - `CON-010`, `CON-011`, `CON-012`, `CON-013`, `CON-014`, `CON-015`, `CON-016`, `CON-020`
- Treatment Planning:
  - `PLN-001`, `PLN-002`, `PLN-003`, `PLN-004`, `PLN-005`, `PLN-006`
- Re-review & Prescription:
  - `RX-001`, `RX-004`
- QA & Double-check:
  - `QA-002`, `QA-003`, `QA-004`, `QA-005`, `QA-008`

Current active count in `All`: 29 transitions.

### 2.2 Important design consequences

- The old contour-review/rework loop is no longer primary in active defaults.
- Treatment (`TRT-*`) and post-treatment (`POST-*`) transitions are not in the active seeded subset at present.
- Legacy definitions still exist in code for backward compatibility/document history, but are not part of active default seed unless added through admin catalog management.

---

## 3. Gate Validation

`GateValidationService` maps named gate checks to concrete delegates and evaluates all declared checks (no short-circuit) to return a complete failure set.

Key properties:

- Unknown gate name is treated as failure (`not implemented`) instead of pass-through.
- Several names are aliases that map to the same implementation.
- Slot-dependent checks (for example S4/S5/S7 checks) resolve profile policy lazily through `IWorkflowProfileResolver`.

---

## 4. External Event Ingestion

There are three inbound paths:

1. Generic integration endpoint: `POST /api/integration/events`
   - Controller: `ExternalEventsController`
   - Dispatches via `IExternalEventDispatcher`
   - Optional bus mode (`Messaging:InboundViaBus=true`) publishes to broker and returns `202`

2. CT endpoint: `POST /api/integration/ct/image-stored`
   - Controller: `CtIntegrationController`
   - Calls `CaseWorkflowService.HandleCtImageStoredAsync`

3. PvMed endpoint: `POST /api/integration/pvmed/events`
   - Controller: `PvMedIntegrationController`
   - Calls `CaseWorkflowService.HandlePvMedEventAsync`

### 4.1 Dispatcher idempotency model

`ExternalEventDispatcher` applies inbox-first reservation:

1. Try reserve `(integration, externalEventId)` in `ExternalEventInbox`
2. If duplicate, return (idempotent no-op)
3. Handle event
4. Write `ExternalEvent` row (`Processed` or `Failed`)
5. Mark inbox row processed
6. Save unit of work

It still performs a legacy `ExternalEventExistsAsync` check for back-compat dedupe.

---

## 5. CT Flow: State-Tolerant Design

The CT image-stored flow was changed to tolerate ordering races.

### 5.1 Current behavior in `HandleCtImageStoredAsync`

- If same external event already marked `Processed`: no-op (idempotent).
- If case already at or beyond `ImageStored`: no-op, optionally record processed event if first seen late.
- If case is `SimInProgress`:
  - event is accepted and stored as pending with `ProcessStatus = PendingSimCompleted`
  - no transition yet
- If case is `SimCompleted`:
  - process immediately:
    - set image references
    - transition to `ImageStored` via `StoreImage`
    - enqueue contour outbox + create `AutoContourMonitor` when auto-contour is enabled
    - transition to `AutoContouringInProgress` via `StartAutoContouring`
    - mark/create external event as `Processed`

### 5.2 Replay trigger

When daily scan completion transitions `SimInProgress -> SimCompleted`, `CompleteDailyImageScanAsync` calls `ProcessPendingCtImageStoredEventsAsync`.

- It scans case CT events for non-processed rows.
- Attempts to deserialize payload and process first valid pending event.
- This provides eventual processing for early-arrival CT callbacks without manual replay.

---

## 6. PvMed Flow (Current)

`HandlePvMedEventAsync` supports:

- `PVMED_AUTOCONTOUR_COMPLETED`
  - from `AutoContouringInProgress` (or legacy `ContouringInProgress`)
  - closes `AutoContourMonitor`
  - granular path:
    - `AutoContouringInProgress -> AutoContouringCompleted`
    - `AutoContouringCompleted -> ManualContouringInProgress`
    - ensure manual contouring work item
  - legacy single-bucket path still supported for in-flight legacy cases

- `PVMED_AUTOCONTOUR_FAILED`
  - from `AutoContouringInProgress` or legacy `ContouringInProgress`
  - rejects `AutoContourMonitor`
  - transitions to `ManualContouringInProgress`
  - optionally ensures manual contouring work item based on S1 fallback policy

All handled PvMed events are persisted to `ExternalEvent` with `ProcessStatus = Processed`.

---

## 7. Outbox Delivery and Compensation

### 7.1 Outbox worker loop

`OutboxWorker` polls every 10 seconds for `New` or retryable `Retrying` messages and dispatches by `DeliveryMode`:

- `Bus` -> `IOutboxPublisher.PublishAsync`
- `Http` -> integration clients (`PvMed`, `Monaco`, `MSQ`)

Retry policy is action-based via `OutboxRetryPolicyMap`. On final exhaustion, message is marked `Failed`.

### 7.2 Compensation escalation

On exhausted retry, worker escalates selected actions:

- `SendImagesToContourTool` -> failed step `IMG-002`
- `SendToMonacoImport` -> failed step `IMG-002`
- `SyncSchedule` -> failed step `TRT-001`
- `GeneratePrescription` -> failed step `RX-006`

Escalation calls `IWorkflowCompensationService.HandleFailureAsync`.

### 7.3 Compensation catalog state

`WorkflowCompensationCatalog.All` currently includes:

- `CMP-002`, `CMP-003`, `CMP-004`, `CMP-005`, `CMP-006`, `CMP-007`, `CMP-009`, `CMP-010`, `CMP-019`, `CMP-020`

So the effective compensation set is a focused subset, not the full historical CMP-001..CMP-020 matrix.

---

## 8. Config Slots (S1-S8)

Policy slots remain runtime-configurable through profile/rule resolution.

- S1 contouring strategy is actively used in CT/PvMed orchestration.
- S3 plan dispatch policy is used when creating planning dispatch work items.
- S4/S5/S7 are consulted by gate checks and/or workflow operations where applicable.
- S6 cancel policy gates cancellation behavior.
- S8 exception handling feeds fallback/retry semantics.

Resolver behavior:

- select most specific active profile (hospital/site/department fallback chain)
- choose highest-priority effective enabled rule per slot
- deserialize typed config and validate
- fallback to built-in defaults when no valid rule applies

---

## 9. Runtime Catalog Storage

### 9.1 Transition catalog

- Tables: `WorkflowTransition`, `WorkflowTransitionFromStatus`, `WorkflowTransitionAttribute`, `WorkflowTransitionChangeLog`
- Service: `WorkflowTransitionCatalogService`
- Admin API: `/api/workflow-transitions`

### 9.2 Workflow vocabulary

- Tables: `WorkflowVocabularyTerm`, `WorkflowVocabularyChangeLog`
- Kinds: `Role`, `WorkItemType`, `CaseFormType`
- Service: `WorkflowVocabularyCatalogService`
- Admin API: `/api/workflow-vocabulary`

### 9.3 Case-status overlay

- Table: `WorkflowCaseStatusOverlay`
- Service: `CaseStatusOverlayService`
- Admin API: `/api/case-status-overlays`
- Cosmetic only, no change-log table

### 9.4 Meta endpoint

- `GET /api/workflow-meta`
- Merges static catalogs with DB vocabulary and status overlay for admin-facing metadata.

---

## 10. Messaging and Sagas

When RabbitMQ is configured:

- MassTransit consumers are enabled for outbox publishing and inbound bridge.
- In-memory outbox is enabled on consumers to avoid partial side-effects within a consume cycle.
- `ContouringSaga` state machine is persisted via EF in `WfmgrDbContext` with optimistic concurrency.

When RabbitMQ is not configured:

- No-op bus publishers are registered.
- API runs in HTTP-only delivery mode.

---

## 11. Security and Operational Notes

- Workflow admin endpoints are protected by policy `WorkflowConfigAdmin` (claim `permission=workflow-config.edit`).
- Integration callback controllers currently include TODO comments for stronger auth (API key or mTLS).
- In local development, active API/debug sessions can lock `Wfmgr.Api/bin` outputs and block test/build copy steps for API projects.

---

## 12. Known Drift Guards

To keep this document accurate:

- Treat `WorkflowTransitionCatalog.All` as the active default transition set.
- Treat `WorkflowCompensationCatalog.All` as the active default compensation set.
- Validate endpoint contracts against controllers under `Wfmgr.Api/Controllers`.
- Validate persistence/table names against `Wfmgr.Infrastructure/Persistence/Configurations`.
