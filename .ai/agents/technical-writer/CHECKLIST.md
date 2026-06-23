# Technical Writer Checklist

## Before Starting

* [ ] Read `.ai/project-context.md`
* [ ] Read `.ai/agent-rules.md`
* [ ] Read `.ai/working-agreement.md`
* [ ] Read `.ai/monthly-plan.md`
* [ ] Read `.ai/handoffs/current-handoff.md`
* [ ] Read the relevant task file under `.ai/tasks/`, if provided
* [ ] Read the relevant source report, implementation report, review report, or validation report
* [ ] Check `git status`
* [ ] Check `.ai/locks`
* [ ] Confirm the documentation scope

## Source Verification

* [ ] Identify the source material for the documentation
* [ ] Confirm whether the implementation is validated
* [ ] Confirm whether tests were run
* [ ] Confirm whether the doc should say "completed", "proposed", or "pending validation"
* [ ] Do not rely only on previous chat memory
* [ ] Use current repository files as source of truth when possible

## Documentation Scope Check

* [ ] Confirm whether this is an ADR, architecture note, runbook, demo script, README update, report cleanup, or handoff update
* [ ] Avoid mixing multiple doc types in one file
* [ ] Keep the document focused
* [ ] Do not include unrelated implementation details
* [ ] Do not add marketing language to engineering docs unless requested

## Safety Check

* [ ] Do not modify source code
* [ ] Do not modify tests
* [ ] Do not modify database schema
* [ ] Do not add EF migrations
* [ ] Do not change secrets
* [ ] Do not change deployment configuration
* [ ] Do not modify CI/CD pipelines
* [ ] Do not edit generated docs under `docs/workflow/generated/`

## ADR Check

For ADRs:

* [ ] Include status
* [ ] Include context
* [ ] Include decision
* [ ] Include alternatives considered
* [ ] Include consequences
* [ ] Keep it short
* [ ] Avoid implementation noise
* [ ] Link to relevant docs or files if useful

## Runbook Check

For runbooks:

* [ ] Include prerequisites
* [ ] Include commands
* [ ] Include expected results
* [ ] Include troubleshooting notes
* [ ] Include rollback or recovery notes when relevant
* [ ] Avoid vague instructions

## Demo Script Check

For demo scripts:

* [ ] Define audience
* [ ] Define demo goal
* [ ] Define setup
* [ ] Define steps
* [ ] Define talking points
* [ ] Define expected result
* [ ] Keep the script short enough to rehearse

## Final Output Check

Before finishing:

* [ ] Summarize files changed
* [ ] State documentation source material
* [ ] State whether implementation was validated
* [ ] State whether build/tests were confirmed
* [ ] List risks or follow-ups
* [ ] Recommend the next step
* [ ] Update `.ai/handoffs/current-handoff.md` if meaningful work was completed

## Stop Conditions

Stop and ask for review if:

* [ ] The implementation is not validated but the doc must say it is completed
* [ ] Source material conflicts with code
* [ ] The requested doc requires technical decisions not yet made
* [ ] The task requires modifying code
* [ ] The task requires modifying tests
* [ ] The task requires editing generated documentation
* [ ] The task requires deployment or production claims that are not verified
