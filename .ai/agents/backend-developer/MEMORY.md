# Backend Developer Memory

## Repository

Repository: `wfmgr`

## Backend Stack

* .NET 10
* ASP.NET Core Web API
* Entity Framework Core
* PostgreSQL
* Docker Compose
* xUnit backend tests

## Backend Projects

* `Wfmgr.Api`
* `Wfmgr.Application`
* `Wfmgr.Domain`
* `Wfmgr.Infrastructure`
* `Wfmgr.Engine`
* `Wfmgr.Contracts`
* `Wfmgr.Api.Tests`

## Important Commands

Build:

```bash
dotnet build wfmgr.sln -c Debug
```

Run backend tests:

```bash
dotnet test Wfmgr.Api.Tests/Wfmgr.Api.Tests.csproj
```

Start PostgreSQL:

```bash
docker compose up -d postgres
```

Run API:

```bash
dotnet run --project Wfmgr.Api/Wfmgr.Api.csproj
```

## Known Environment Notes

Daniel's macOS project path:

```text
/Users/daniel/Projects/wfmgr
```

Hermes Docker project path:

```text
/workspace/wfmgr
```

Docker maps:

```text
/Users/daniel/Projects -> /workspace
```

Some Hermes Docker environments may not have the .NET SDK installed. If `dotnet` is unavailable, do not claim tests were run. Provide the exact commands Daniel should run locally.

## Important Database Rule

The project supports both:

* `database/init.sql`
* EF Core migrations

Do not combine both bootstrap approaches on the same database unless the migration state is clearly managed.

Do not add or apply EF Core migrations without explicit approval.

## Workflow Configurability Principle

Workflow behavior should be configurable where business variation is expected.

Backend changes should avoid hardcoding:

* Workflow statuses as business rules
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
* External event routing behavior

## Current Month 1 Context

Month 1 focus:

```text
Workflow hardcoding inspection and first safe backend refactor.
```

The first refactor candidate is outbox routing consolidation.

Current design direction:

```text
IOutboxRoutingPolicy = delivery mode / transport policy
IOutboxRouteProvider = target system + outbox action + optional message type route mapping
```

The goal is to remove duplicated outbox route mappings between:

* `WorkflowSideEffectService`
* `WorkflowCompensationService`

without changing behavior.

## Lessons Learned

* Do not rely on old session memory. Always read current files.
* Do not use vague prompts like "continue previous task."
* Always inspect `git diff` before reviewing or stabilizing existing changes.
* Quality Engineer should not implement code.
* Backend Developer should stabilize implementation and fix compile/test issues.
* If a change was accidentally made by another role, treat it as unreviewed implementation and review it carefully before continuing.

## Current Known Validation Commands

```bash
dotnet build wfmgr.sln -c Debug
dotnet test Wfmgr.Api.Tests/Wfmgr.Api.Tests.csproj
```
