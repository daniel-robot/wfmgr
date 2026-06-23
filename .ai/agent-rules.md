# Agent Rules

## Purpose

This document defines how AI agents should work in the `wfmgr` repository.

The goal is to use AI agents as safe engineering assistants, not as uncontrolled autonomous developers.

Agents should help with repository inspection, workflow configuration review, hardcoded workflow logic detection, test suggestion, documentation generation, security review, deployment runbook generation, and architecture review.

## Core Principles

1. Inspect before modifying.
2. Explain findings before proposing changes.
3. Prefer small incremental changes.
4. Do not redesign the system unless explicitly asked.
5. Do not modify production-like configuration without approval.
6. Do not run destructive commands.
7. Do not expose secrets, keys, tokens, or credentials.
8. Always explain risk and impact.
9. Always suggest tests for behavior changes.
10. Keep workflow behavior configurable where business variation is expected.
11. Prefer specific file-level findings over broad opinions.
12. Prefer small next steps over large rewrites.
13. Do not assume intended business behavior if the code does not show it.

## Agent Coordination Rules

Before editing files, agents must check:

```text
.ai/locks
```

Agents should update the current handoff after meaningful work:

```text
.ai/handoffs/current-handoff.md
```

Agent private workspaces are under:

```text
~/.openclaw/workspace/<agent-id>
```

Project memory is under:

```text
.ai
```

Role-specific memory is under:

```text
.ai/agents/<agent-id>
```

Agents should clearly state:

* What they inspected
* What they changed, if anything
* What they did not change
* What tests were run
* What risks remain
* What the next recommended step is

## Permission Levels

### L0 - Read Only

Allowed without approval:

* Read source code
* Read documentation
* Read test files
* Read configuration examples
* Read database initialization scripts
* Read Docker Compose files
* Read build files
* Read prompt files
* Run safe inspection commands such as `ls`, `find`, `rg`, `cat`, and `git status`

### L1 - Recommendation

Allowed without approval:

* Generate findings
* Generate reports
* Suggest tests
* Suggest refactoring
* Suggest documentation updates
* Suggest architecture improvements
* Suggest security controls

### L2 - Controlled Write

Requires explicit human approval:

* Modify source code
* Modify tests
* Modify documentation
* Modify workflow configuration
* Create or update prompt files
* Create example reports
* Create non-destructive helper scripts

### L3 - High-Risk Operation

Requires explicit human approval and clear explanation of risk:

* Modify database schema
* Add EF Core migrations
* Run deployment commands
* Delete files
* Change secrets or credentials
* Change production-like configuration
* Push commits
* Force reset git state
* Modify CI/CD pipelines
* Run commands affecting external services

## Allowed Read-Only Actions

Agents may read:

* Source code
* Documentation
* Test files
* Configuration examples
* Database initialization scripts
* Docker Compose files
* Build files
* Prompt files

## Restricted Actions

Agents must not perform these actions without explicit approval:

* Modify source code
* Modify database schema
* Add EF Core migrations
* Delete files
* Run deployment commands
* Change secrets
* Change production configuration
* Push commits
* Force reset git state
* Modify CI/CD pipelines
* Modify external services
* Modify files under generated documentation folders unless explicitly instructed

## Database Rules

The repository supports both `database/init.sql` and EF Core migrations, but they should not be combined on the same database unless the migration state is clearly managed.

Agents must not:

* Apply EF Core migrations to a database initialized with `database/init.sql` unless explicitly approved.
* Modify database schema without explaining compatibility impact.
* Generate migrations without approval.
* Assume a database bootstrap strategy if the current environment is unclear.

## Generated Documentation Rules

Generated workflow documentation under this path should not be edited manually:

```text
docs/workflow/generated/
```

If generated workflow documentation is outdated, agents should recommend regenerating it instead of manually editing generated files.

## Workflow Configuration Principles

Agents should flag hardcoded workflow behavior when found.

Examples of hardcoded behavior:

* Fixed workflow statuses
* Fixed stage names
* Fixed transition paths
* Fixed work item types
* Fixed role assignment rules
* Fixed SLA values
* Hospital-specific if/else logic
* Department-specific if/else logic
* Site-specific if/else logic
* Validation rules embedded in services
* Compensation behavior embedded in services
* External event mapping rules embedded in services
* Case lifecycle rules embedded in services
* Work item lifecycle rules embedded in services

Workflow behavior should be configurable where business variation is expected, especially across:

* Hospital
* Site
* Department
* Workflow profile
* Workflow rule
* Work item type
* External event source

## Expected Finding Format

When reporting issues, use this format:

```markdown
## Finding N

- Area:
- File:
- Issue:
- Evidence:
- Risk:
- Recommendation:
- Suggested test:
- Priority:
```

## Priority Definition

### High

The issue blocks workflow configurability, creates major maintenance risk, or prevents hospital/site/department-specific behavior.

### Medium

The issue creates moderate maintenance risk or should become configurable soon.

### Low

The issue is acceptable for now but should be monitored or documented.

## Report Output Expectations

Agents should produce:

* Clear summary
* Specific file references
* Concrete findings
* Evidence from inspected files
* Risk explanation
* Recommended small next step
* Suggested tests
* Follow-up inspection suggestion

Agents should avoid:

* Vague recommendations
* Large rewrites
* Unverified assumptions
* Hidden behavior changes
* Over-engineered proposals
* Reporting findings without evidence

## Example Report Location

Repository inspection example reports should be saved under:

```text
docs/agent/examples/
```

The first hardcoded workflow inspection report should use this path:

```text
docs/agent/examples/hardcode-inspection-report.md
```

## Human Approval Required

Human approval is required for:

* Any code modification
* Any schema modification
* Any deployment operation
* Any destructive command
* Any secret/configuration change
* Any action affecting external services
* Any operation that changes repository state beyond generating approved documentation or reports

## Test Expectations

For behavior changes, agents should suggest relevant tests.

Examples:

* API integration tests
* Application service unit tests
* Domain rule tests
* Workflow configuration validation tests
* Frontend component tests
* Frontend service tests

Agents should not claim a change is safe unless relevant tests have been suggested or run.

## Final Response Expectations

At the end of each task, agents should include:

```markdown
## Summary

## Files Inspected

## Files Changed

## Tests Run

## Risks / Notes

## Recommended Next Step
```

If no files were changed, say so explicitly.
