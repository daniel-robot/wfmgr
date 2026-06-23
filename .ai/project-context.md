# Project Context

## Repository

- wfmgr

## Product Goal

This repository implements a configurable workflow management system for radiotherapy workflow orchestration.

The goal is to support configurable workflow profiles, workflow rules, work item lifecycle management, case lifecycle management, external events, auditability, and future AI-assisted workflow analysis.

Workflow behavior should be configurable where business variation is expected, especially across hospital, site, department, workflow profile, and workflow rule scopes.

## Primary Stack

- .NET 10 / ASP.NET Core Web API
- Angular 21
- PostgreSQL
- Docker Compose

## Backend

ASP.NET Core Web API in `Wfmgr.Api` with supporting projects:

* `Wfmgr.Application`
* `Wfmgr.Domain`
* `Wfmgr.Infrastructure`
* `Wfmgr.Engine`
* `Wfmgr.Contracts`

## Frontend

Angular 21 standalone app in `wfmgr-ui` using npm 11 and TypeScript 5.9.

## Database

PostgreSQL 14+

## Preferred Bootstrap

`database/init.sql` via Docker Compose or psql.

## Important Database Rule

EF Core migrations are also supported, but `init.sql` and EF Core migrations should not be combined on the same database.

Use one bootstrap strategy per database:

* Use `database/init.sql` for Docker Compose or manual psql initialization.
* Use EF Core migrations only on databases where migration history is clearly managed.

## Key Domain Concepts

### Case

A `Case` represents a radiotherapy workflow case.

Important fields may include:

* CaseId
* Hospital
* Site
* Department
* PatientId
* ExternalKey
* CurrentStatus
* StatusVersion
* CreatedAt
* UpdatedAt

### WorkItem

A `WorkItem` represents a unit of work in the workflow.

Important fields may include:

* WorkItemId
* CaseId
* Type
* Status
* AssignedRole
* AssignedUserId
* DueAt
* SlaMinutes
* ExternalCorrelationId
* PayloadJson
* CreatedAt
* UpdatedAt

### ExternalEvent

An `ExternalEvent` represents an event coming from an external system.

Important fields may include:

* EventId
* Source
* Type
* ExternalId
* CaseCorrelationKey
* PayloadJson
* CreatedAt

### WorkflowProfile

A `WorkflowProfile` defines workflow configuration scope and behavior.

Expected scope fields:

* Hospital
* Site
* Department

Expected configuration areas:

* Workflow stages
* Work item types
* Role assignment rules
* SLA rules
* Transition rules
* Validation rules
* Compensation behavior

### WorkflowRule

A `WorkflowRule` defines configurable workflow behavior.

Examples:

* Status transition rule
* Work item creation rule
* Assignment rule
* SLA rule
* Validation rule
* Compensation rule

## Workflow Configurability Principle

Workflow behavior should be configurable where business variation is expected.

Agents should flag hardcoded workflow behavior when found.

Avoid hardcoding:

* Workflow statuses
* Stage names
* Work item types
* Transition rules
* Role assignments
* SLA values
* Hospital-specific behavior
* Site-specific behavior
* Department-specific behavior
* Validation rules
* Compensation behavior
* External event mapping rules
* Case lifecycle rules
* Work item lifecycle rules

## Test Commands

Backend tests:

```bash
dotnet test Wfmgr.Api.Tests/Wfmgr.Api.Tests.csproj
```

Frontend tests:

```bash
cd wfmgr-ui && npm test
```

## Run Commands

```bash
docker compose up -d postgres
dotnet restore
dotnet build wfmgr.sln -c Debug
dotnet run --project Wfmgr.Api/Wfmgr.Api.csproj
cd wfmgr-ui && npm install && npm start
```

## Known Constraints

* Requires .NET SDK 10.0.x
* Requires Node.js 20+
* Requires npm 11+
* Requires PostgreSQL 14+
* Backend development URL is `http://localhost:5223`
* Frontend development URL is `http://localhost:4200`
* Angular environment files assume the API runs on port 5223
* Workflow logic beyond `PlanningPending` is not fully implemented yet
* Generated workflow docs under `docs/workflow/generated/` should not be edited manually

## Path Mapping

* macOS/OpenClaw project path: `/Users/daniel/Projects/wfmgr`
* Hermes Docker project path: `/workspace/wfmgr`
* Docker maps `/Users/daniel/Projects` to `/workspace`

## Agent Coordination Rules

* Agent private workspaces are under `~/.openclaw/workspace/<agent-id>`
* Project memory is under `/Users/daniel/Projects/wfmgr/.ai`
* Role-specific memory is under `/Users/daniel/Projects/wfmgr/.ai/agents/<agent-id>`
* Agents must check `.ai/locks` before editing files
* Agents should update `.ai/handoffs/current-handoff.md` after meaningful work

## Agent Usage Goal

AI agents should help with:

* Repository inspection
* Workflow configuration review
* Hardcoded workflow logic detection
* Test suggestion
* Documentation generation
* Security review
* Deployment runbook generation
* Architecture review
* Refactoring recommendation

AI agents should not directly make high-risk changes unless explicitly approved by a human.

## Agent Safety Boundary

Agents may perform read-only inspection without approval.

Read-only actions include:

* Reading source code
* Reading documentation
* Reading test files
* Reading configuration examples
* Reading database initialization scripts
* Reading Docker Compose files
* Reading build files
* Reading prompt files

Agents must not perform these actions without explicit human approval:

* Modify source code
* Modify database schema
* Add EF Core migrations
* Delete files
* Run deployment commands
* Change secrets
* Change production-like configuration
* Push commits
* Force reset git state
* Modify CI/CD pipelines

Agents should always explain:

* What they inspected
* What they found
* Why it matters
* What risk exists
* What small next step is recommended
* What tests should be added or updated

## Current Agile Team Roles

* Product Owner
* Architect
* Project Manager
* Quality Engineer
* DevOps Engineer
* Frontend Developer
* Backend Developer
* Technical Writer
