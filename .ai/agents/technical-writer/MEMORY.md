# Technical Writer Memory

## Repository

Repository: `wfmgr`

## Documentation Goal

The documentation should support Daniel's six-month plan:

```text
Secure AI Agent + Azure + Enterprise Workflow
```

The project should become a clear portfolio showing how AI agents can be safely integrated into enterprise workflow software.

## Current Documentation Structure

Important `.ai` files:

* `.ai/README.md`
* `.ai/project-context.md`
* `.ai/agent-rules.md`
* `.ai/working-agreement.md`
* `.ai/monthly-plan.md`
* `.ai/active-sprint.md`
* `.ai/backlog.md`
* `.ai/handoffs/current-handoff.md`

Important docs areas:

* `docs/architecture/`
* `docs/architecture/decisions/`
* `docs/security/`
* `docs/runbooks/`
* `docs/agent/examples/`
* `docs/demo/`

## Current Month 1 Context

Month 1 focus:

```text
Workflow hardcoding inspection and first safe backend refactor.
```

Completed or in progress:

* Architect completed hardcoding inspection.
* Inspection report identified outbox routing duplication.
* Quality Engineer reviewed testability.
* Backend Developer reviewed and stabilized the route provider direction.
* Final validation reported PASS.
* Daniel still needs to run local build and backend tests before commit.

Current design direction:

```text
IOutboxRoutingPolicy = delivery mode / transport policy
IOutboxRouteProvider = target system + outbox action + optional message type route mapping
```

## Current Documentation Candidates

Likely next documents:

```text
docs/architecture/decisions/ADR-001-consolidate-outbox-routing.md
docs/architecture/outbox-routing.md
docs/agent/examples/hardcode-inspection-report.md
.ai/handoffs/current-handoff.md
```

## Documentation Rules

* Do not document a change as completed until it is validated.
* If build/tests were not run, say so clearly.
* Do not claim tests passed unless the user or agent output confirms it.
* Do not edit generated docs under `docs/workflow/generated/`.
* Keep ADRs short and decision-focused.
* Keep runbooks step-by-step and command-focused.
* Keep demo scripts audience-friendly.

## Known Agent Workflow

Default flow:

```text
Product Owner / Project Manager
→ Architect
→ Quality Engineer
→ Backend or Frontend Developer
→ Quality Engineer
→ Technical Writer
→ Project Manager
```

Technical Writer usually acts after implementation and validation.

## Lessons Learned

* Avoid vague prompts like "continue previous task."
* Always use concrete file paths.
* Quality Engineer should not modify production code.
* Backend Developer handles implementation and stabilization.
* Technical Writer should document after validation, not before.
* The `.ai/handoffs/current-handoff.md` file should reflect the current next step.
