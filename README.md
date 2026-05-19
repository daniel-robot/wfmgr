# wfmgr

Production-style radiotherapy workflow management system. .NET 10 Web API backend with Angular 21 testing console frontend.

---

## Solution Structure

```bash
wfmgr.sln
├── Wfmgr.Domain          — Core entities, domain enums (CaseStatus, WorkItemStatus, …)
├── Wfmgr.Application     — Use-case services, abstractions, request/response DTOs
├── Wfmgr.Infrastructure  — EF Core + PostgreSQL, persistence, outbox, integration 
├── Wfmgr.Contracts       — Contracts with external systems/applications 
adapters
├── Wfmgr.Api             — ASP.NET Core Web API, controllers, background OutboxWorker
└── wfmgr-ui/             — Angular 21 standalone testing console (separate npm project)

database/
└── init.sql              — Full schema DDL + seed data (WorkflowProfile + rules)
```

### Case Status Flow

```bash
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
| ------------- | --------- |
| .NET SDK | 10.0.x |
| Node.js | 20+ |
| npm | 11+ |
| PostgreSQL | 14+ (or Docker) |
| Docker + Docker Compose | any recent version |

---

## 1. Database Initialization

### Option A — Docker Compose (recommended)

To reset the database from scratch:

```bash
docker compose down -v
docker compose up -d postgres
```

pgAdmin is also available at `http://localhost:5050` (email: `admin@wfmgr.com`, password: `admin`).

### Option B — Manual psql against an existing PostgreSQL instance

```bash
psql -h localhost -U postgres -d WfmgrDb -f database/init.sql
```

### Option C — EF Core Migrations

Use this only if you are not using `init.sql`. Do not apply both to the same database.

Linux/macOS (bash):

```bash
dotnet tool restore

dotnet tool run dotnet-ef database update \
  --project Wfmgr.Infrastructure/Wfmgr.Infrastructure.csproj \
  --startup-project Wfmgr.Api/Wfmgr.Api.csproj \
  --context WfmgrDbContext
```

Windows PowerShell:

```powershell
dotnet tool restore

dotnet tool run dotnet-ef database update `
  --project Wfmgr.Infrastructure/Wfmgr.Infrastructure.csproj `
  --startup-project Wfmgr.Api/Wfmgr.Api.csproj `
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

## 4. Configuration Reference

### Backend (`Wfmgr.Api/appsettings.json`)

| Key | Description | Default |
| ----- | ------------- | --------- |
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

## 5. Build

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

```bash
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
| ------- | ------------- |
| `phases` | Nine workflow phases, each with `id`, `number`, `name`, `codeRange` |
| `statuses` | All 40 `CaseStatus` values, each assigned to a phase |
| `transitions` | All 53 `TransitionDefinition` entries (code, fromStatuses, toStatus, triggerType, role, gates, …) |
| `compensations` | All 20 `CompensationDefinition` entries (code, failedStepCode, targetStatus, retryPolicy, …) |

The JSON is the single source of truth for diagram generation. The C# catalogs (`WorkflowTransitionCatalog`, `WorkflowCompensationCatalog`) remain the runtime source of truth for the application.
