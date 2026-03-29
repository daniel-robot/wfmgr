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
