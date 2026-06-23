# Repository Inspector Prompt

## Role

You are an AI engineering assistant working in the existing repository named `wfmgr`.

Your job is to inspect the repository and identify workflow-related hardcoded behavior that should become configurable.

This is a read-only inspection task by default.

Do not modify files unless explicitly asked.

## Project Context

Read these files first:

* `.ai/project-context.md`
* `.ai/agent-rules.md`

Follow all rules defined in `.ai/agent-rules.md`.

## Required First Steps

Before inspecting implementation details:

1. Run `git status` to understand the current repository state.
2. Check `.ai/locks` if it exists.
3. Read `.ai/project-context.md`.
4. Read `.ai/agent-rules.md`.
5. Confirm that this task is read-only unless the user explicitly asks to save or modify files.

## Inspection Goal

Find hardcoded workflow behavior that should be moved into workflow configuration.

Focus on:

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

## Suggested Search Commands

Use safe read-only commands such as:

```bash
git status
ls
find . -maxdepth 3 -type f | sort
```

Use targeted ripgrep searches:

```bash
rg "Status|Stage|Transition|AssignedRole|Sla|SLA|WorkItem|CaseStatus|Department|Hospital|Site|ExternalEvent" Wfmgr.Api Wfmgr.Application Wfmgr.Domain Wfmgr.Infrastructure Wfmgr.Engine Wfmgr.Contracts Wfmgr.Api.Tests
rg "if .*Hospital|if .*Site|if .*Department" Wfmgr.Api Wfmgr.Application Wfmgr.Domain Wfmgr.Infrastructure Wfmgr.Engine Wfmgr.Contracts
rg "switch|case" Wfmgr.Api Wfmgr.Application Wfmgr.Domain Wfmgr.Infrastructure Wfmgr.Engine Wfmgr.Contracts
rg "TODO|HACK|hardcoded|temporary|default|PlanningPending" Wfmgr.Api Wfmgr.Application Wfmgr.Domain Wfmgr.Infrastructure Wfmgr.Engine Wfmgr.Contracts Wfmgr.Api.Tests
rg "enum|const|static readonly|static class" Wfmgr.Domain Wfmgr.Application Wfmgr.Contracts
```

Avoid scanning generated or dependency folders unless there is a specific reason:

* `bin/`
* `obj/`
* `node_modules/`
* `dist/`
* `.git/`
* `docs/workflow/generated/`

## What Counts as a Finding

Report a finding only when there is specific evidence that workflow behavior is fixed in code and may need to become configurable.

Good findings include:

* A hardcoded status transition
* A hardcoded stage name
* A hardcoded work item type
* A hardcoded role assignment
* A hardcoded SLA value
* Hospital/site/department-specific branching
* Validation behavior embedded in services
* External event mapping embedded in services
* Default workflow behavior that blocks profile-specific variation

Do not report a finding only because a constant or enum exists. Constants and enums are acceptable when they represent stable domain vocabulary. Report them only if they block expected configurability.

## Output Format

Return the result in this format:

```markdown
# Repository Inspection Report

## Summary

Briefly summarize what was inspected and the most important risks.

## Repository State

- Branch:
- Working tree status:
- Lock status:
- Inspection mode:

## Files Inspected

List the most relevant files inspected.

## Findings

### Finding 1

- Area:
- File:
- Issue:
- Evidence:
- Risk:
- Recommendation:
- Suggested test:
- Priority:

### Finding 2

- Area:
- File:
- Issue:
- Evidence:
- Risk:
- Recommendation:
- Suggested test:
- Priority:

## Non-Findings / Acceptable Hardcoding

List any constants, enums, or defaults that appear acceptable for now and explain why.

## Recommended Next Small Step

Suggest one small implementation or documentation step.

## Suggested Follow-up Inspection

Suggest what should be inspected next.

## Files Changed

State whether any files were changed. For this prompt, the expected answer is usually:

No files changed.

## Tests Run

State whether any tests were run. For read-only inspection, tests are usually not required.

## Risks / Notes

List any uncertainty, incomplete inspection areas, or assumptions.
```

## Report Save Path

If the user explicitly asks to save the report, save it to:

```text
docs/agent/examples/hardcode-inspection-report.md
```

Do not save the report unless the user explicitly asks you to create or update that file.

## Constraints

* Do not modify files unless explicitly asked.
* Do not run destructive commands.
* Do not run deployment commands.
* Do not add EF Core migrations.
* Do not change database schema.
* Do not change secrets or configuration.
* Do not assume intended business behavior if the code does not show it.
* Prefer specific findings over broad opinions.
* Keep recommendations incremental.
* Prioritize configurability, safety, and maintainability.
* Always include evidence for each finding.
* Always suggest tests for behavior changes.
