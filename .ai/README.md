# AI Project Memory

This `.ai` folder is the shared project memory and coordination space for OpenClaw agents on macOS and Hermes inside Docker.

It helps multiple agents work safely in the same repository by providing:

* Project context
* Agent rules
* Role definitions
* Agent-specific memory
* Task files
* Handoffs
* Locks
* Sprint/backlog tracking
* Checklists
* Monthly execution plan
* Lessons learned

## Path Mapping

macOS / OpenClaw path:

```text
/Users/daniel/Projects/<repo>
```

Hermes Docker path:

```text
/workspace/<repo>
```

For this repository:

```text
/Users/daniel/Projects/wfmgr
/workspace/wfmgr
```

## Required Reading Order

Before starting meaningful work, agents should read:

1. `.ai/project-context.md`
2. `.ai/agent-rules.md`
3. `.ai/working-agreement.md`
4. `.ai/monthly-plan.md`
5. `.ai/active-sprint.md`
6. `.ai/backlog.md`
7. `.ai/handoffs/current-handoff.md`
8. The relevant task file under `.ai/tasks/`
9. The relevant role file under `.ai/roles/`
10. The relevant agent folder under `.ai/agents/<agent-name>/`

## Key Files

| File                          | Purpose                                                                    |
| ----------------------------- | -------------------------------------------------------------------------- |
| `project-context.md`          | Main repository context, stack, commands, constraints, and domain overview |
| `agent-rules.md`              | Safety rules and behavior expectations for agents                          |
| `working-agreement.md`        | Team working rules and collaboration expectations                          |
| `monthly-plan.md`             | Six-month execution plan and current monthly checkpoint                    |
| `active-sprint.md`            | Current sprint or short-term execution focus                               |
| `backlog.md`                  | Future candidate tasks                                                     |
| `handoffs/current-handoff.md` | Current state of work and next agent handoff                               |
| `status/daily-status.md`      | Lightweight status tracking                                                |
| `status/risk-log.md`          | Risks, blockers, and mitigation notes                                      |

## Directory Structure

```text
.ai/
├── agents/
├── roles/
├── tasks/
├── handoffs/
├── locks/
├── checklists/
├── decisions/
├── lessons-learned/
├── shared/
├── status/
├── project-context.md
├── agent-rules.md
├── working-agreement.md
├── monthly-plan.md
├── active-sprint.md
└── backlog.md
```

## Agents

Agent-specific files live under:

```text
.ai/agents/<agent-name>/
```

Each agent folder may include:

* `README.md`
* `MEMORY.md`
* `CHECKLIST.md`
* Role-specific templates

Examples:

```text
.ai/agents/architect/
.ai/agents/backend-developer/
.ai/agents/frontend-developer/
.ai/agents/quality-engineer/
.ai/agents/devops-engineer/
.ai/agents/product-owner/
.ai/agents/project-manager/
.ai/agents/technical-writer/
```

## Roles

Role definitions live under:

```text
.ai/roles/
```

Role files describe what each agent is responsible for.

Examples:

```text
.ai/roles/architect.md
.ai/roles/backend-developer.md
.ai/roles/quality-engineer.md
.ai/roles/technical-writer.md
```

## Tasks

Task files live under:

```text
.ai/tasks/
```

Each task file should be self-contained and include:

* Goal
* Required context
* Scope
* Constraints
* Expected output
* Stop conditions

Agents should prefer task files over vague instructions like:

```text
continue previous task
```

Good example:

```text
.ai/tasks/inspect-workflow-hardcoding.md
```

## Handoffs

The current handoff file is:

```text
.ai/handoffs/current-handoff.md
```

Agents should use it when passing work to another role.

A good handoff should include:

* Current task
* Previous agent output
* Decisions made
* Files changed
* Tests run
* Open questions
* Risks
* Next agent
* Next agent instructions
* Stop conditions

## Locks

Locks live under:

```text
.ai/locks/
```

Agents must check locks before editing files.

Use locks to avoid multiple agents modifying the same files at the same time.

Recommended lock naming:

```text
.ai/locks/<agent-name>-<task-name>.lock
```

Example:

```text
.ai/locks/backend-developer-outbox-routing.lock
```

A lock should include:

```markdown
# Lock

## Owner Agent

## Task

## Files Locked

## Started At

## Expected Release

## Notes
```

## Checklists

Shared checklists live under:

```text
.ai/checklists/
```

Use them as quality gates.

Examples:

```text
.ai/checklists/file-conflict-check.md
.ai/checklists/handoff-ready.md
.ai/checklists/test-review.md
.ai/checklists/release-readiness.md
```

## Decisions

Agent/team operating decisions can be stored under:

```text
.ai/decisions/
```

Software architecture ADRs should be stored under:

```text
docs/architecture/decisions/
```

Use this separation:

```text
.ai/decisions/ = agent/team operating decisions
docs/architecture/decisions/ = software architecture decisions
```

## Monthly Plan

The six-month execution plan is tracked in:

```text
.ai/monthly-plan.md
```

This file should remain a living roadmap.

It should track:

* Current month goal
* Completed work
* In-progress work
* Pending work
* Success criteria
* Next step

## Default Agent Flow

For meaningful engineering work, use this default flow:

```text
Product Owner / Project Manager
→ Architect
→ Quality Engineer
→ Backend Developer or Frontend Developer
→ Quality Engineer
→ Technical Writer
→ Project Manager
```

Not every task needs every role.

Use the minimum number of agents needed to produce a safe, reviewable artifact.

## Role Boundaries

| Agent              | Should Do                            | Should Not Do                     |
| ------------------ | ------------------------------------ | --------------------------------- |
| Product Owner      | Define value and acceptance criteria | Implement code                    |
| Project Manager    | Track progress and handoff           | Redesign architecture             |
| Architect          | Analyze design and create ADRs       | Implement routine code            |
| Quality Engineer   | Define tests and validate changes    | Modify production code            |
| Backend Developer  | Implement backend changes            | Redesign scope without approval   |
| Frontend Developer | Implement frontend changes           | Change backend contracts casually |
| DevOps Engineer    | Review deployment and infrastructure | Change application logic          |
| Technical Writer   | Update docs and demo scripts         | Change code behavior              |

## Safety Rules

Agents must:

* Inspect before modifying.
* Check `.ai/locks` before editing files.
* Stay within task scope.
* Preserve existing behavior unless explicitly asked to change it.
* Avoid destructive commands.
* Avoid exposing secrets.
* Avoid modifying production-like configuration.
* Avoid database schema changes unless explicitly approved.
* Avoid editing generated docs under `docs/workflow/generated/`.

## Build and Test Commands

Backend build:

```bash
dotnet build wfmgr.sln -c Debug
```

Backend tests:

```bash
dotnet test Wfmgr.Api.Tests/Wfmgr.Api.Tests.csproj
```

Frontend tests:

```bash
cd wfmgr-ui && npm test
```

If an agent environment does not have the required tools, the agent must state that clearly and provide the exact command Daniel should run locally.

## Current Operating Rule

Every meaningful task should produce at least one concrete artifact:

* A task file
* A prompt
* A report
* A small refactor
* A test
* An ADR
* A runbook
* A demo script
* A handoff update

The goal is to make the AI agent workflow repeatable, reviewable, and safe.