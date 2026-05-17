# wfmgr

Production-style radiotherapy workflow management system. .NET 10 Web API backend with Angular 21 testing console frontend.

---

## Solution Structure

```
wfmgr.sln
├── Wfmgr.Domain          — Core entities, domain enums (CaseStatus, WorkItemStatus, …)
├── Wfmgr.Application     — Use-case services, abstractions, request/response DTOs
├── Wfmgr.Infrastructure  — EF Core + PostgreSQL, persistence, outbox, integration adapters
├── Wfmgr.Api             — ASP.NET Core Web API, controllers, background OutboxWorker
└── wfmgr-ui/             — Angular 21 standalone testing console (separate npm project)

database/
└── init.sql              — Full schema DDL + seed data (WorkflowProfile + rules)
```

### API Endpoints

| Method | Route | Purpose |
|--------|-------|---------|
| `POST` | `/api/cases` | Create a new workflow case |
| `GET`  | `/api/cases` | List all cases |
| `GET`  | `/api/cases/{caseId}` | Get case details |
| `POST` | `/api/cases/{caseId}/sim-record` | Submit simulation record |
| `POST` | `/api/cases/{caseId}/forward/monaco` | Manually forward to Monaco |
| `GET`  | `/api/cases/{caseId}/work-items` | Get work items for a case |
| `GET`  | `/api/cases/{caseId}/audit-logs` | Get audit log for a case |
| `GET`  | `/api/cases/{caseId}/transition-history` | Get case transition history |
| `GET`  | `/api/cases/{caseId}/forms` | Get all forms for a case |
| `GET`  | `/api/cases/{caseId}/attachments` | Get case attachments |
| `GET`  | `/api/cases/{caseId}/external-events` | Get case external/inbox events |
| `GET`  | `/api/cases/{caseId}/integration-references` | Get integration references by case |
| `GET`  | `/api/cases/{caseId}/plan-versions` | Get plan versions by case |
| `GET`  | `/api/workflow/statuses` | Get available workflow statuses |
| `GET`  | `/api/workflow/work-item-types` | Get available workflow work item types |
| `POST` | `/api/integration/ct/image-stored` | Receive CT image stored event |
| `POST` | `/api/integration/pvmed/events` | Receive PvMed autocontour event |
| `GET`  | `/api/audit-logs` | Get all audit logs |

### Case Status Flow

```
Draft → Submitted
  → SimScheduled → SimInProgress → SimCompleted
  → ImageStored → ImageForwarding
  → ContouringInProgress → ContoursReady → ContoursUnderReview
      ↳ ContoursRejected → ContourReworkRequired (loop back)
  → PlanningPending → PlanningAssigned → PlanningInProgress → PlanReady
  → PlanUnderReview → PlanReviewed → [PlanReReviewOptional]
  → PrescriptionGenerating → PrescriptionReady → [PrescriptionSyncFailed]
  → PlanQAInProgress → PlanQAApproved → [PlanDoubleCheckOptional]
  → ReadyForScheduling → SchedulingInProgress → Scheduled
  → OrderPending → OrderSubmitted → QueuePending
  → Treating → [TreatmentPaused | TreatmentInterrupted] → TreatmentCompleted
  → PostTreatmentReviewPending → PostTreatmentReviewed
  → Archived
      ↳ Cancelled (from any status)
```

> **Developer note — expanded lifecycle (March 2026):** The domain was extended from
> the original 6-status partial workflow to the full 40-status radiotherapy lifecycle.
> The former `MonacoForwarded` status has been replaced by `PlanningPending` — all
> existing "forward to Monaco" code transitions now target `PlanningPending`.
> The `GetActiveAsync` repository query now excludes both `Archived` and `Cancelled`
> instead of the old `MonacoForwarded` terminal state.
> Workflow logic for the new statuses beyond `PlanningPending` is not yet implemented;
> add handlers in `CaseWorkflowService` as each phase is built out.

---

## Prerequisites

| Requirement | Version |
|-------------|---------|
| .NET SDK | 10.0.x |
| Node.js | 20+ |
| npm | 11+ |
| PostgreSQL | 14+ (or Docker) |
| Docker + Docker Compose | any recent version |

---

## 1. Database Initialization

### Option A — Docker Compose (recommended)

Starts PostgreSQL 16 and applies `database/init.sql` automatically on first boot:

```bash
docker compose up -d postgres
```

The init script creates all tables, indexes, and seeds a default `WorkflowProfile` for `HOSP001 / SITE_A / RT`.

pgAdmin is also available at `http://localhost:5050` (email: `admin@wfmgr.com`, password: `admin`).

To reset the database from scratch:

```bash
docker compose down -v
docker compose up -d postgres
```

### Option B — Manual psql against an existing PostgreSQL instance

```bash
psql -h localhost -U postgres -d WfmgrDb -f database/init.sql
```

### Option C — EF Core Migrations

Use this only if you are not using `init.sql`. Do not apply both to the same database.

```bash
dotnet tool restore

dotnet tool run dotnet-ef database update \
  --project Wfmgr.Infrastructure/Wfmgr.Infrastructure.csproj \
  --startup-project Wfmgr.Api/Wfmgr.Api.csproj \
  --context WfmgrDbContext
```

To create a new migration after schema changes:

```bash
dotnet tool run dotnet-ef migrations add <MigrationName> \
  --project Wfmgr.Infrastructure/Wfmgr.Infrastructure.csproj \
  --startup-project Wfmgr.Api/Wfmgr.Api.csproj \
  --context WfmgrDbContext \
  --output-dir Persistence/Migrations
```

---

## 1b. Messaging (RabbitMQ — optional)

The outbox supports two delivery modes per action: **HTTP** (synchronous, the default) and
**Bus** (asynchronous via RabbitMQ + MassTransit). The bus is opt-in — without configuration
the API runs in HTTP-only mode and any action accidentally routed to the bus fails fast.

### Start RabbitMQ

```bash
docker compose up -d rabbitmq
```

Management UI: `http://localhost:15672` (user `wfmgr` / password `wfmgr`).

### Enable the bus for the API

Configuration is read from `RabbitMq:Host` and `Messaging:BusActions`. The committed
`appsettings.json` ships with the host unset; enable per-run via env vars:

```bash
export RabbitMq__Host=localhost
export RabbitMq__Username=wfmgr
export RabbitMq__Password=wfmgr
export Messaging__BusActions__0=SendToMonacoImport

dotnet run --project Wfmgr.Api/Wfmgr.Api.csproj
```

### Health probe

| Route | Returns |
|-------|---------|
| `GET /health` | Liveness (process up) |
| `GET /health/ready` | Readiness (all registered checks) |
| `GET /health/messaging` | JSON: publisher mode (`bus` / `http-only`) + implementation type |

### Switching an action to the bus

1. Add the outbox action name to `Messaging:BusActions`.
2. Ensure an `IConsumer<T>` exists in `Wfmgr.Infrastructure/Integrations/Messaging/Consumers/`
   and is registered in `AddMessaging()` in `Wfmgr.Infrastructure/DependencyInjection.cs`.
3. The next outbox row stamped with that action will be published to RabbitMQ instead of
   dispatched over HTTP. Existing in-flight HTTP rows finish on the HTTP path.

### Routing inbound webhooks through the bus

Set `Messaging:InboundViaBus=true` to make `POST /api/integration/events` publish each
request to RabbitMQ instead of dispatching inline. The `IngestExternalEventConsumer`
replays it through `IExternalEventDispatcher`, which still uses the inbox table
(`ExternalEventInbox`) for idempotency — so broker redelivery is safe.

```bash
export Messaging__InboundViaBus=true
```

This converts the controller into a thin "accept-and-publish" endpoint, letting an
inbound flood drain at the consumer's pace instead of holding HTTP connections open.

### Saga: `ContouringSaga` (Phase 3)

The contour → import handshake runs as a MassTransit state-machine saga so that
"which step is this case in" has a single durable home outside the case row.

| State | Triggered by | Transitions to |
| --- | --- | --- |
| `Initial` | `StartContouringSaga.V1` (published by `StartContouringSagaRelay` when `SendImagesToContourTool.V1` is observed) | `AwaitingContour` |
| `AwaitingContour` | `ContourCompleted.V1` (translated by `SagaExternalEventTranslatorConsumer` from `IngestExternalEvent.V1` of type `contour.completed`) | `AwaitingMonacoAck` |
| `AwaitingMonacoAck` | `MonacoImportAcked.V1` (translated from external events of type `monaco.import.acked`) | Final (row removed) |

Saga instances are persisted in the `ContouringSagaState` table inside `WfmgrDbContext`,
using MassTransit's EF Core repository with optimistic concurrency. Correlation id =
case id, so every event for the same case lands on the same instance.

> **Timeouts are not yet wired.** `Schedule(...)` requires the RabbitMQ
> `delayed_message_exchange` plugin or a Quartz scheduler. The `TimeoutTokenId`
> column is reserved for that wiring. Until then the saga relies on broker
> retries + manual operator intervention for stuck cases.

### Production hardening (Phase 5)

**Transactional consume.** Every bus consumer runs inside MassTransit's
`InMemoryOutbox`: messages a consumer publishes via `IPublishEndpoint` are buffered
and released only after the consumer completes successfully. A consumer throw
discards the buffered messages — no partial side-effects on retry.

**Dead-letter monitoring.** `GET /health/messaging` scrapes the RabbitMQ management
API for `*_error` queue depth and reports per-queue counts plus a total. Thresholds
are configurable:

```jsonc
"RabbitMq": {
  "ManagementPort": 15672,
  "ManagementScheme": "http",
  "DeadLetterDegradedThreshold": 1,    // any DLQ message → Degraded
  "DeadLetterUnhealthyThreshold": 100  // 100+ DLQ messages → Unhealthy
}
```

A failed scrape reports Degraded (publisher still works; only observability is impaired).

**Broker metrics.** The local-dev stack ships a Prometheus container scraping
RabbitMQ's built-in `rabbitmq_prometheus` plugin:

```bash
docker compose up -d rabbitmq prometheus
open http://localhost:9090       # Prometheus UI
open http://localhost:15692/metrics  # raw exporter
```

Useful queries:
- `rabbitmq_queue_messages{queue=~".*_error"}` — DLQ depth per endpoint
- `rabbitmq_queue_messages_ready` — work backlog
- `rabbitmq_channel_consumers` — confirms wfmgr is connected

**Example `/health/messaging` response (bus mode, one stuck message):**

```json
{
  "status": "Degraded",
  "results": {
    "messaging": {
      "status": "Degraded",
      "data": {
        "mode": "bus",
        "dlq.total": 1,
        "dlq.queues": 1,
        "dlq.send-images-to-contour-tool_error": 1
      }
    }
  }
}
```

If the management API itself is unreachable the check stays Degraded (publisher still
works; only observability is impaired) and surfaces `data.probeError`.

**Smoke-testing the hardening locally:**

```bash
# 1. DLQ probe — force a message into <queue>_error (e.g. by temporarily throwing
#    in a consumer), then:
curl -s http://localhost:5223/health/messaging | jq

# Clear it afterwards:
docker exec -it wfmgr-rabbitmq rabbitmqctl purge_queue <name>_error

# 2. Prometheus scrape target is up
curl -s 'http://localhost:9090/api/v1/targets' \
  | jq '.data.activeTargets[] | {job:.labels.job, health, lastError}'

# 3. InMemoryOutbox — temporarily Publish() + throw inside a consumer; verify the
#    published message never appears on any downstream queue across all retries.
```

---

## 2. Start the Backend

```bash
# From solution root
dotnet restore
dotnet build wfmgr.sln -c Debug
dotnet run --project Wfmgr.Api/Wfmgr.Api.csproj
```

The API listens on **`http://localhost:5223`** in development (see `Wfmgr.Api/Properties/launchSettings.json`).

Swagger UI is available at: **`http://localhost:5223/swagger`**

Configuration is in `Wfmgr.Api/appsettings.Development.json`. Key settings:

```json
{
  "ConnectionStrings": {
    "WfmgrDb": "Host=localhost;Port=5432;Database=WfmgrDb;Username=postgres;Password=postgres"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:4200"]
  }
}
```

---

## 3. Start the Frontend

```bash
cd wfmgr-ui
npm install
npm start
```

Open **`http://localhost:4200`** in your browser.

The Angular dev server uses `src/environments/environment.development.ts` which points to `http://localhost:5223`. If your API runs on a different port, update that file.

See `wfmgr-ui/README.md` for full frontend documentation.

---

## 4. End-to-End Workflow Testing

### Prerequisites

- API running on `http://localhost:5223`
- Database initialized with seed data (WorkflowProfile for HOSP001/SITE_A/RT)
- Frontend running on `http://localhost:4200` (optional — curl works standalone)

---

### Step 1 — Create a Case

```bash
curl -s -X POST http://localhost:5223/api/cases \
  -H "Content-Type: application/json" \
  -d '{
    "hospitalId": "HOSP001",
    "siteId": "SITE_A",
    "departmentId": "RT",
    "accessionNumber": "ACC-2024-001",
    "patientId": "PAT-001",
    "notes": "Test workflow run"
  }' | jq .
```

**Response:** `{ "caseId": "<uuid>" }`  
**Case status:** `Submitted` — a `SIM_RECORD` work item is created for the SimTech role.

Save the returned `caseId`:

```bash
CASE_ID="<uuid from response>"
```

---

### Step 2 — Submit Simulation Record

```bash
curl -s -X POST http://localhost:5223/api/cases/$CASE_ID/sim-record \
  -H "Content-Type: application/json" \
  -d '{
    "ctMachineId": "CT-01",
    "simulatedAt": "2024-03-28T09:00:00.000Z",
    "recordFormJson": "{\"operator\":\"sim-tech-01\",\"kVp\":120,\"mAs\":200}"
  }'
```

**Response:** `204 No Content`  
**Case status:** `SimCompleted` — `SIM_RECORD` work item is closed.

---

### Step 3 — Simulate CT Image Stored Event

This event is keyed by `accessionNumber` (not `caseId`):

```bash
curl -s -X POST http://localhost:5223/api/integration/ct/image-stored \
  -H "Content-Type: application/json" \
  -d '{
    "externalEventId": "evt-ct-001",
    "accessionNumber": "ACC-2024-001",
    "dicomRef": {
      "studyInstanceUid": "1.2.840.113619.2.55.3.604688435.1234.1711111111.1",
      "seriesInstanceUids": ["1.2.840.113619.2.55.3.604688435.1234.1711111111.2"],
      "modality": "CT"
    },
    "dicomWebLocation": {
      "wadoRsUrl": "https://dicom.example.local/wado-rs/studies/1.2.3"
    },
    "occurredAt": "2024-03-28T09:30:00.000Z"
  }'
```

**Response:** `202 Accepted`  
**Case status:** `ContouringInProgress` — an outbox message is queued to PvMed (or a `MANUAL_CONTOURING` work item is created, depending on the workflow profile).

---

### Step 4 — Simulate PvMed Autocontour Completed Event

```bash
curl -s -X POST http://localhost:5223/api/integration/pvmed/events \
  -H "Content-Type: application/json" \
  -d "{
    \"externalEventId\": \"evt-pvmed-001\",
    \"caseId\": \"$CASE_ID\",
    \"type\": \"PVMED_AUTOCONTOUR_COMPLETED\",
    \"pvMedJob\": {
      \"jobId\": \"pvmed-job-001\",
      \"status\": \"Completed\",
      \"progress\": 100
    },
    \"pvMedResult\": {
      \"rtStructLocation\": {
        \"studyInstanceUid\": \"1.2.840.113619.2.55.3.604688435.1234.1711111111.1\",
        \"seriesInstanceUid\": \"1.2.840.113619.2.55.3.604688435.1234.1711111111.3\"
      }
    },
    \"occurredAt\": \"2024-03-28T10:00:00.000Z\"
  }"
```

**Response:** `202 Accepted`  
**Case status:** `ContoursReady` → `PlanningPending` (if the workflow profile has `autoForwardToMonaco: true`), or a `MANUAL_FORWARD_TO_MONACO` work item is created.

---

### Step 5 — Verify Final State

```bash
# Inspect the case
curl -s http://localhost:5223/api/cases/$CASE_ID | jq .

# Inspect work items
curl -s http://localhost:5223/api/cases/$CASE_ID/work-items | jq .

# Inspect audit trail
curl -s http://localhost:5223/api/cases/$CASE_ID/audit-logs | jq .

# Inspect transition history
curl -s http://localhost:5223/api/cases/$CASE_ID/transition-history | jq .

# Inspect forms and integration metadata
curl -s http://localhost:5223/api/cases/$CASE_ID/forms | jq .
curl -s http://localhost:5223/api/cases/$CASE_ID/external-events | jq .
curl -s http://localhost:5223/api/cases/$CASE_ID/integration-references | jq .

# Inspect plan versions / attachments
curl -s http://localhost:5223/api/cases/$CASE_ID/plan-versions | jq .
curl -s http://localhost:5223/api/cases/$CASE_ID/attachments | jq .

# Inspect workflow helper catalogs
curl -s http://localhost:5223/api/workflow/statuses | jq .
curl -s http://localhost:5223/api/workflow/work-item-types | jq .
```

### Step 6 — Validate in Angular Workflow Console

Open `http://localhost:4200/cases/$CASE_ID` and verify the following UI sections refresh with data:

- Work Items
- Audit Timeline
- Transition History
- Forms
- External Events
- Integration References
- Plan Versions
- Attachments
- Workflow Status Catalog
- Work Item Type Catalog

Use **Form Action Tester** in the same page to create+submit a form and confirm new entries appear in Forms and Transition History after refresh.

Use **Advanced Workflow Actions** to test failure/retry/rework/cancel behavior from the same screen:

- Restart Contouring
- Reject Contour Review
- Reject Plan Review
- Reject Plan Re-review
- Mark/Retry/Resolve Prescription Sync
- Fail QA
- Mark/Retry Scheduling Failure
- Pause/Interrupt/Resume Treatment
- Cancel Case

---

### Optional — Manual Forward to Monaco

If the workflow profile does not auto-forward, trigger it manually:

```bash
curl -s -X POST http://localhost:5223/api/cases/$CASE_ID/forward/monaco
```

**Case status:** `PlanningPending`

---

## Configuration Reference

### Backend (`Wfmgr.Api/appsettings.json`)

| Key | Description | Default |
|-----|-------------|---------|
| `ConnectionStrings:WfmgrDb` | PostgreSQL connection string | `Host=localhost;Port=5432;…` |
| `Cors:AllowedOrigins` | Allowed CORS origins | `["http://localhost:4200"]` |
| `PvMed:BaseUrl` | PvMed service base URL | `https://pvmed.local` |
| `Monaco:DropRoot` | Directory for Monaco import drop files | `/var/wfmgr/monaco-drop` |

Development overrides live in `appsettings.Development.json` and are merged automatically when `ASPNETCORE_ENVIRONMENT=Development`.

### Frontend (`wfmgr-ui/src/environments/`)

| File | Used when |
|------|-----------|
| `environment.development.ts` | `npm start` / `ng serve` |
| `environment.ts` | `ng build` (production build) |

---

## 5. Workflow Engine Architecture

The application layer contains a structured, catalog-driven workflow engine built across two releases:

- **v2 (March 2026)** — Transition & compensation catalogs, gate validation service.
- **v3 (April 2026)** — `CaseTransitionService`: the central execution engine that ties catalog lookup, role gating, gate validation, and audit persistence into a single service. `CaseWorkflowService` now routes all transitions through it.

### 5.1 Transition Catalog

**`Wfmgr.Application.Workflows.V1.Definitions.TransitionDefinition`**

Immutable record that describes a single named state transition:

| Field | Type | Purpose |
|-------|------|---------|
| `Code` | `string` | Unique code, e.g. `SIM-001` |
| `FromStatuses` | `CaseStatus[]` | Valid source statuses |
| `ToStatus` | `CaseStatus` | Target status on success |
| `TriggerName` | `string` | Command / event name |
| `TriggerType` | `WorkflowTriggerType` | User / System / ExternalEvent / Timer |
| `RequiredRole` | `string?` | Slash-separated role list, or `null` for system triggers |
| `GateChecks` | `string[]` | Named pre-conditions (resolved by `GateValidationService`) |
| `SuccessActions` | `string[]` | Side-effect descriptions on success |
| `FailureActions` | `string[]` | Side-effect descriptions on gate failure |
| `WorkItemsToCreate` | `string[]` | Work item types to create on success |
| `ConfigSlot` | `string?` | Workflow profile slot code (S1–S8), or `null` |

**`Wfmgr.Application.Workflows.V1.WorkflowTransitionCatalog`**

Static catalog of **44 transitions** covering the full radiotherapy lifecycle:

| Phase | Codes |
|-------|-------|
| Intake & Simulation | SIM-001 … SIM-005 |
| Image Acquisition | IMG-001 … IMG-003 |
| Contouring | CON-001 … CON-005 |
| Contour Review | REV-001 … REV-004 |
| Treatment Planning | PLN-001 … PLN-006 |
| Re-review & Prescription | RX-001 … RX-007 |
| Plan QA & Double-check | QA-001 … QA-008 |
| Scheduling, Order & Treatment | TRT-001 … TRT-012 |
| Post-treatment & Archiving | POST-001 … POST-003 |

Lookup:

```csharp
var t = WorkflowTransitionCatalog.ByCode["SIM-001"];
foreach (var t in WorkflowTransitionCatalog.All) { ... }
```

### 5.2 Compensation Catalog

**`Wfmgr.Application.Workflows.V1.Definitions.CompensationDefinition`**

Immutable record that describes the recovery action when a workflow step fails:

| Field | Type | Purpose |
|-------|------|---------|
| `Code` | `string` | Unique code, e.g. `CMP-001` |
| `FailedStepCode` | `string` | Transition code of the failing step |
| `FailureCondition` | `string` | Human-readable failure description |
| `CompensationAction` | `string` | Action to take |
| `TargetStatus` | `CaseStatus?` | Status to restore, or `null` for no-op |
| `WorkItemToCreate` | `string?` | Work item type to open for manual resolution |
| `ManualInterventionRequired` | `bool` | Whether human action is needed |
| `RetryPolicy` | `RetryPolicy?` | Retry strategy (`ExponentialBackoff`, `LimitedRetry`, `TimerEscalation`) |

**`Wfmgr.Application.Workflows.V1.WorkflowCompensationCatalog`**

Static catalog of **20 compensation rules** (CMP-001 … CMP-020).

Lookup:

```csharp
var c = WorkflowCompensationCatalog.ByCode["CMP-001"];
var rules = WorkflowCompensationCatalog.ByFailedStep["IMG-002"];
```

### 5.3 Gate Validation Service

**`IGateValidationService`** (registered as scoped DI service)

```csharp
public interface IGateValidationService
{
    Task<GateValidationResult> ValidateAsync(
        CaseData caseData,
        TransitionDefinition transition,
        GateValidationContext context,
        CancellationToken ct = default);
}
```

**`GateValidationContext`** — caller-supplied context:

| Property | Type | Purpose |
|----------|------|---------|
| `UserId` | `string?` | ID of the acting user |
| `Roles` | `IReadOnlyCollection<string>` | Roles of the acting user |
| `FormId` | `Guid?` | Submitted form accompanying the transition |
| `WorkItemId` | `Guid?` | Work item being completed |
| `ExternalEventPayload` | `string?` | Raw external event JSON |
| `Reason` | `string?` | Rejection / cancellation reason |
| `Metadata` | `IReadOnlyDictionary<string, object?>` | Arbitrary key-value pairs (see `GateCheckNames.Meta*` constants) |

Factory helpers: `GateValidationContext.FromTransitionContext(ctx)`, `GateValidationContext.System()`.

**`GateValidationResult`** — rich result:

```csharp
result.IsValid          // bool
result.FailedChecks     // IReadOnlyList<string> — names of failing checks
result.Messages         // IReadOnlyList<string> — human-readable reasons
result.ToSummary()      // formatted string for logging
```

**Named gate checks** (all string constants on `GateCheckNames`):

| Category | Gate check names |
|----------|------------------|
| Simulation | `SimulationRequestFormValid`, `SimulationRecordFormValid`, `SimulationScheduleExists` |
| Case state | `CaseNotCancelled`, `CancellationAllowed`, `TreatmentNotStarted` |
| Image | `ImageReferenceExists`, `ImageAccessible`, `CaseResolvedByCorrelationKey` |
| Contouring | `ContourResultExists`, `EventIdempotent`, `ManualContourPayloadValid`, `RetryAllowed` |
| Review | `ReviewApprovalExists`, `RejectionReasonRequired` |
| Planning | `PlanVersionExists`, `PlanEvaluationApproved`, `AssigneeExists`, `TaskAssigned` |
| Re-review | `S4ReReviewEnabled`, `NoReReviewRequired`, `ReReviewApproved` |
| Prescription | `PrescriptionReferenceExists` |
| QA | `QAFormApproved`, `PlanAndPrescriptionPresent`, `DoubleCheckApproved` |
| Scheduling | `S5DoubleCheckEnabled`, `S5DoubleCheckDisabled`, `ScheduleReferenceExists` |
| Treatment | `TreatmentOrderFormValid`, `TreatmentCompletionSatisfied`, `MedicalApprovalExists` |
| Post-treatment | `PostTreatmentReviewFormValid`, `NoBlockingTasks`, `RequiredFormsComplete` |

**Example usage:**

```csharp
// Inject IGateValidationService
var transition = WorkflowTransitionCatalog.ByCode["REV-002"];
var ctx = new GateValidationContext
{
    UserId = currentUserId,
    Roles  = userRoles,
    Reason = request.Reason,
};
var result = await _gateValidationService.ValidateAsync(caseData, transition, ctx, ct);
if (!result.IsValid)
    return BadRequest(result.ToSummary());
```

> The gate validation service is invoked automatically by `CaseTransitionService` for every
> catalog-matched transition. You do not need to call `IGateValidationService` directly
> unless building custom pre-condition checks outside the standard transition flow.

### 5.4 Transition Execution Service

**`ICaseTransitionService`** (registered as scoped DI service)

```csharp
public interface ICaseTransitionService
{
    Task<TransitionExecutionResult> ApplyTransitionAsync(
        Guid caseId, string triggerName, GateValidationContext context,
        CancellationToken ct = default, CaseStatus? fallbackToStatus = null);

    Task<TransitionExecutionResult> ApplyTransitionAsync(
        CaseData caseData, string triggerName, GateValidationContext context,
        CancellationToken ct = default, CaseStatus? fallbackToStatus = null);
}
```

For each call the service executes these steps in order:

1. Looks up the `WorkflowTransitionCatalog` by `triggerName` + `caseData.CurrentStatus`.
2. Checks `RequiredRole` (slash-separated list against `context.Roles`).
3. Runs `IGateValidationService.ValidateAsync` for all declared `GateChecks`.
4. On success: mutates `caseData.CurrentStatus`, increments `StatusVersion`.
5. Writes `AuditLog` and `CaseTransitionHistory`.
6. Calls `IWorkflowSideEffectService.ExecuteAsync` (catalog-matched transitions only).
7. On failure: returns a structured `TransitionExecutionResult` without mutating state.

When `triggerName` has no catalog entry and `fallbackToStatus` is supplied, steps 1–6 are
skipped and the transition is applied unconditionally (backward-compatible bridge for
call sites not yet mapped to a catalog code).

**`TransitionExecutionResult`**

```csharp
result.IsSuccess        // bool
result.TransitionCode   // string? — null when applied via fallback path
result.FromStatus       // CaseStatus
result.ToStatus         // CaseStatus? — null on failure
result.FailureReason    // TransitionFailureReason? (NotFound | RoleDenied | GateCheckFailed)
result.FailedChecks     // IReadOnlyList<string>
result.Messages         // IReadOnlyList<string>
result.ThrowIfFailed()  // throws InvalidOperationException if !IsSuccess
result.ToSummary()      // formatted string for logging
```

**Example usage:**

```csharp
var gateCtx = new GateValidationContext
{
    UserId = currentUserId,
    Roles  = userRoles,
    FormId = request.FormId,
    Reason = request.Reason,
};
var result = await _caseTransitionService.ApplyTransitionAsync(
    caseData, "SubmitSimulationRequest", gateCtx, ct);
result.ThrowIfFailed();
await _dataAccess.SaveChangesAsync(ct);
```

### 5.5 Transition Side Effects

**`IWorkflowSideEffectService`** (registered as scoped DI service)

```csharp
public interface IWorkflowSideEffectService
{
    Task ExecuteAsync(
        TransitionDefinition definition,
        SideEffectContext context,
        CancellationToken ct = default);
}
```

Called automatically by `CaseTransitionService` after every successful catalog-matched
transition. Does **not** call `SaveChangesAsync` — the caller owns the unit of work.

#### Work item creation

Iterates `TransitionDefinition.WorkItemsToCreate`. For each type:

- **Idempotency guard**: skips if an open work item of that type already exists for the case.
- **Role resolution**: consults the relevant workflow-profile slot (S1–S5) where applicable;
falls back to a static default-role table covering all 23 supported work item types.

| Work item type | Default role | Profile slot |
|---|---|---|
| `SimulationSchedule` | `SimTech/Scheduler` | — |
| `SimulationRecord` | `SimTech` | — |
| `ImageValidation` | `SimTech/Physicist` | — |
| `ImageForwardToContourTool` | `System` | S1 (manual role) |
| `AutoContourMonitor` | `System` | — |
| `ManualContouring` | `Physician/ThirdPartyOperator` | S1 (manual role) |
| `ContourReview` | `Physician` | — |
| `ContourSecondReview` | `Physician/Physicist` | — |
| `ContourRework` | `Physician/ThirdPartyOperator` | S2 (rework role) |
| `PlanAssignment` | `Scheduler/System` | S3 (target role) |
| `PlanDesign` | `Dosimetrist` | S3 (target role) |
| `PlanEvaluation` | `Physicist/Physician` | — |
| `PlanReReview` | `Physician/Physicist` | S4 (review role) |
| `PrescriptionSync` | `Physicist/System` | — |
| `PlanQA` | `Physicist/QAReviewer` | — |
| `PlanDoubleCheck` | `Physicist` | S5 (work item role) |
| `ScheduleSync` | `Scheduler/System` | — |
| `TreatmentOrder` | `Physician` | — |
| `QueueCall` | `System` | — |
| `TreatmentMonitor` | `System` | — |
| `TreatmentExceptionHandling` | `Admin` | — |
| `PostTreatmentReview` | `Physician` | — |
| `ArchiveReview` | `System/Admin` | — |

#### Outbox dispatch

Iterates `TransitionDefinition.SuccessActions`. Recognised action strings are mapped to
`OutboxActions` constants:

| `SuccessActions` string | `OutboxActions` constant | Target system |
|---|---|---|
| `CreateOutboxSendImagesToContourTool` | `SendImagesToContourTool` | S1 provider (default `PvMed`) |
| `CreateOutboxRestartContouring` | `SendImagesToContourTool` | S1 provider |
| `SendToMonacoImport` | `SendToMonacoImport` | `Monaco` |
| `CreateOutboxGeneratePrescription` | `GeneratePrescription` | `PvMed` |
| `CreateOutboxPrescriptionSync` | `GeneratePrescription` | `PvMed` |
| `StartScheduleWatch` | `SyncSchedule` | `MSQ` |
| `CreateTreatmentMonitor` | `QueryTreatmentProgress` | `Monaco` |
| `UpdateProgress` | `QueryTreatmentProgress` | `Monaco` |

Unrecognised action strings are silently ignored, making the map forwards-compatible.

### 5.6 Compensation Service

**`IWorkflowCompensationService`** (registered as scoped DI service)

```csharp
public interface IWorkflowCompensationService
{
    Task<CompensationResult> HandleFailureAsync(
        Guid caseId,
        string failedStepCode,      // e.g. "IMG-002", "RX-006"
        CompensationContext context,
        CancellationToken ct = default);
}
```

**`CompensationContext`** — failure details supplied by the caller:

| Property | Type | Purpose |
|---|---|---|
| `Reason` | `string?` | Human-readable description of what went wrong |
| `UserId` | `string?` | User who triggered the failing action |
| `SourceSystem` | `string?` | Integration that reported the failure (e.g. `"PvMed"`) |
| `ExternalEventPayload` | `string?` | Raw external event JSON |
| `FailedOutboxMessageId` | `Guid?` | Outbox message that failed to deliver |
| `RetryCount` | `int` | Attempts already made (used for retry budget check) |
| `Metadata` | `IReadOnlyDictionary<string, object?>` | Arbitrary structured data |

For each call the service executes these steps:

1. Looks up `WorkflowCompensationCatalog.ByFailedStep[failedStepCode]`.
2. Loads the case (`CaseNotFound` result if missing).
3. **Status change** — when `definition.TargetStatus` differs from the current status, delegates to `ICaseTransitionService.ApplyTransitionAsync` with `fallbackToStatus = definition.TargetStatus` (trigger name `"Compensate:CMP-xxx"`). This ensures `AuditLog` and `CaseTransitionHistory` are written consistently.
4. **Work item creation** — idempotency-guarded via `GetOpenWorkItemAsync`; default-role table covers all 13 compensation work item types.
5. **Outbox retry** — when `RetryPolicy ≠ null` and `context.RetryCount < policy.MaxAttempts`, enqueues an `OutboxMessageData` with computed `NextRetryAt` (exponential back-off or linear).
6. Returns `CompensationResult`.

**`CompensationResult`**

```csharp
result.IsSuccess           // bool
result.CompensationCode    // string?  — e.g. "CMP-008"
result.PreviousStatus      // CaseStatus?
result.NewStatus           // CaseStatus? — null when status unchanged
result.WorkItemCreated     // string?  — WorkItemTypes constant, or null
result.RetryDispatched     // bool
result.FailureReason       // CompensationFailureReason? (DefinitionNotFound | CaseNotFound | WorkItemCreationFailed)
result.ThrowIfFailed()     // throws InvalidOperationException if !IsSuccess
result.ToSummary()         // formatted string for logging
```

**Compensation matrix (CMP-001 – CMP-020):**

| Code | Failed step | Target status | Work item | Retry policy |
|---|---|---|---|---|
| CMP-001 | IMG-002 | `ImageForwarding` | `ImageForwardToContourTool` | ExponentialBackoff |
| CMP-002 | IMG-003 | `ImageStored` | `ImageForwardToContourTool` | LimitedRetry |
| CMP-003 | CON-002 | `ContourReworkRequired` | `ManualContouring` | — |
| CMP-004 | CON-003 | `ContourReworkRequired` | `ManualContouring` | LimitedRetry |
| CMP-005 | REV-003 | `ContoursRejected` | `ContourRework` | — |
| CMP-006 | PLN-005 | `PlanningInProgress` | `PlanDesign` | — |
| CMP-007 | RX-004 | `PlanningInProgress` | `PlanDesign` | — |
| CMP-008 | RX-006 | `PrescriptionSyncFailed` | `PrescriptionSync` | ExponentialBackoff |
| CMP-009 | QA-003 | `PlanQAFailed` | `PlanDesign` | — |
| CMP-010 | QA-008 | `PlanningInProgress` | `PlanDesign` | — |
| CMP-011 | TRT-001 | `SchedulingInProgress` | `ScheduleSync` | ExponentialBackoff |
| CMP-012 | TRT-004 | `OrderPending` | `TreatmentOrder` | — |
| CMP-013 | TRT-005 | `OrderSubmitted` | `QueueCall` | LimitedRetry |
| CMP-014 | TRT-008 | `TreatmentPaused` | `TreatmentExceptionHandling` | TimerEscalation |
| CMP-015 | TRT-010 | `TreatmentInterrupted` | `TreatmentExceptionHandling` | — |
| CMP-016 | TRT-012 | `Treating` | `TreatmentMonitor` | PollingRetry (unlimited) |
| CMP-017 | POST-002 | `PostTreatmentReviewPending` | `PostTreatmentReview` | — |
| CMP-018 | POST-003 | `PostTreatmentReviewed` | `ArchiveReview` | — |
| CMP-019 | SIM-005 | *(unchanged)* | — | — |
| CMP-020 | ANY\_EXTERNAL\_EVENT | *(unchanged)* | — | — |

**Example usage:**

```csharp
// Inject IWorkflowCompensationService
var result = await _compensationService.HandleFailureAsync(
    caseId,
    "RX-006",
    new CompensationContext
    {
        Reason       = "Oncology system returned HTTP 503",
        SourceSystem = "PvMed",
        RetryCount   = 2,
        FailedOutboxMessageId = outboxMessageId,
    },
    ct);
result.ThrowIfFailed();
await _dataAccess.SaveChangesAsync(ct);
```

> **Note:** `HandleFailureAsync` does **not** call `SaveChangesAsync`. The caller owns the
> unit of work and must commit when ready.

### 5.7 Database Schema Impact

All v2–v5 additions are **application-layer only**. No schema changes are required.
All gate checks, side effects, and compensation writes use tables present since v1
(`CaseForm`, `WorkItem`, `OutboxMessage`, `ExternalEvent`, `PlanVersion`, `WorkflowRule`,
`CaseTransitionHistory`). See `database/init.sql` changelog comment for full release history.

---

## Build

### Backend

```bash
dotnet restore
dotnet build wfmgr.sln -c Debug
```

### Frontend

```bash
cd wfmgr-ui
npm install
npm run build
```

---

## 6. Workflow Diagrams

PlantUML state-machine diagrams are auto-generated from a single JSON definition file.

### Structure

```
workflow/
├── workflow-definition.json      ← source of truth (statuses, transitions, compensations)
└── generate-workflow-diagrams.py ← generator script

docs/workflow/generated/          ← output directory (committed, do not edit manually)
├── case-state-machine.puml       ← full 40-status / 53-transition lifecycle
├── simulation-saga.puml          ← Phase 1: Intake & Simulation
├── contouring-saga.puml          ← Phases 2–4: Image Acquisition, Contouring, Review
├── planning-saga.puml            ← Phases 5–7: Planning, Re-review, QA
├── treatment-saga.puml           ← Phases 8–9: Scheduling, Treatment, Archiving
└── compensation-flow.puml        ← All 20 compensation rules (CMP-001 – CMP-020)
```

### Regenerate after changes

Any time `workflow-definition.json` is modified (new status, new transition, updated compensation), regenerate all diagrams with:

```bash
cd workflow/
python3 generate-workflow-diagrams.py
```

To regenerate a single diagram:

```bash
python3 generate-workflow-diagrams.py simulation-saga
python3 generate-workflow-diagrams.py compensation-flow
# etc.
```

Available diagram names: `case-state-machine`, `simulation-saga`, `contouring-saga`, `planning-saga`, `treatment-saga`, `compensation-flow`.

### Render diagrams

Use any PlantUML renderer, for example:

- **VS Code**: install the [PlantUML](https://marketplace.visualstudio.com/items?itemName=jebbs.plantuml) extension, then open a `.puml` file and press `Alt+D`.
- **Online**: paste content into [plantuml.com/plantuml](https://www.plantuml.com/plantuml/uml).
- **CLI**: `java -jar plantuml.jar docs/workflow/generated/*.puml`

### Workflow definition schema

`workflow-definition.json` contains four top-level arrays:

| Array | Description |
|-------|-------------|
| `phases` | Nine workflow phases, each with `id`, `number`, `name`, `codeRange` |
| `statuses` | All 40 `CaseStatus` values, each assigned to a phase |
| `transitions` | All 53 `TransitionDefinition` entries (code, fromStatuses, toStatus, triggerType, role, gates, …) |
| `compensations` | All 20 `CompensationDefinition` entries (code, failedStepCode, targetStatus, retryPolicy, …) |

The JSON is the single source of truth for diagram generation. The C# catalogs (`WorkflowTransitionCatalog`, `WorkflowCompensationCatalog`) remain the runtime source of truth for the application.
