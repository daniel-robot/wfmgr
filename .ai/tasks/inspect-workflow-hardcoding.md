# Task: Inspect Workflow Hardcoding

## Goal

Inspect the current `wfmgr` repository and identify workflow-related hardcoded behavior that should become configurable.

This is a read-only inspection task.

Do not modify files.

## Required Context

Read these files first:

* `.ai/project-context.md`
* `.ai/agent-rules.md`
* `prompts/repository-inspector.md`

Follow the rules in `.ai/agent-rules.md`.

Use `prompts/repository-inspector.md` as the main inspection prompt.

## Required First Steps

Before inspecting implementation details:

1. Run:

```bash
git status
```

2. Check whether `.ai/locks` exists:

```bash
ls -la .ai
ls -la .ai/locks || true
```

3. Confirm this is read-only inspection mode.

## Inspection Scope

Inspect these projects first:

* `Wfmgr.Domain`
* `Wfmgr.Application`
* `Wfmgr.Infrastructure`
* `Wfmgr.Engine`
* `Wfmgr.Api`
* `Wfmgr.Contracts`
* `Wfmgr.Api.Tests`

Frontend inspection is optional for this first pass. Focus first on backend workflow behavior.

## Focus Areas

Find hardcoded workflow behavior related to:

* Workflow statuses
* Workflow stages
* Work item types
* Status transitions
* Role assignment rules
* SLA values
* Validation rules
* Compensation behavior
* Hospital-specific logic
* Site-specific logic
* Department-specific logic
* External event handling rules
* Case lifecycle rules
* Work item lifecycle rules
* Workflow profile selection rules
* Workflow rule evaluation logic
* Default workflow behavior
* Planning-specific behavior that may need to become configurable later

## Suggested Commands

Use safe read-only commands only.

```bash
git status
find . -maxdepth 3 -type f | sort
```

Run targeted searches:

```bash
rg "Status|Stage|Transition|AssignedRole|Sla|SLA|WorkItem|CaseStatus|Department|Hospital|Site|ExternalEvent" Wfmgr.Api Wfmgr.Application Wfmgr.Domain Wfmgr.Infrastructure Wfmgr.Engine Wfmgr.Contracts Wfmgr.Api.Tests

rg "if .*Hospital|if .*Site|if .*Department" Wfmgr.Api Wfmgr.Application Wfmgr.Domain Wfmgr.Infrastructure Wfmgr.Engine Wfmgr.Contracts

rg "switch|case" Wfmgr.Api Wfmgr.Application Wfmgr.Domain Wfmgr.Infrastructure Wfmgr.Engine Wfmgr.Contracts

rg "TODO|HACK|hardcoded|temporary|default|PlanningPending" Wfmgr.Api Wfmgr.Application Wfmgr.Domain Wfmgr.Infrastructure Wfmgr.Engine Wfmgr.Contracts Wfmgr.Api.Tests

rg "enum|const|static readonly|static class" Wfmgr.Domain Wfmgr.Application Wfmgr.Contracts
```

Avoid scanning these folders unless needed:

* `bin/`
* `obj/`
* `node_modules/`
* `dist/`
* `.git/`
* `docs/workflow/generated/`

## Expected Output

Generate a repository inspection report using the format defined in:

```text
prompts/repository-inspector.md
```

The report must include:

* Summary
* Repository state
* Files inspected
* Findings
* Non-findings / acceptable hardcoding
* Recommended next small step
* Suggested follow-up inspection
* Files changed
* Tests run
* Risks / notes

## Important Constraints

Do not modify files.

Do not create or update `docs/agent/examples/hardcode-inspection-report.md` unless explicitly asked after the report is reviewed.

Do not add migrations.

Do not change database schema.

Do not run deployment commands.

Do not run destructive commands.

Do not expose secrets.

## Final Answer Requirements

At the end, clearly state:

```markdown
## Summary

## Files Inspected

## Files Changed

## Tests Run

## Risks / Notes

## Recommended Next Step
```

Expected value for this task:

```text
Files Changed: No files changed.
```
