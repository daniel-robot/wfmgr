# Backend Developer Checklist

## Before Starting

* [ ] Read `.ai/project-context.md`
* [ ] Read `.ai/agent-rules.md`
* [ ] Read `.ai/working-agreement.md`
* [ ] Read `.ai/handoffs/current-handoff.md`
* [ ] Read the task file under `.ai/tasks/`, if provided
* [ ] Check `git status`
* [ ] Check `.ai/locks`
* [ ] Identify files likely to be changed
* [ ] Confirm the task is backend-scoped
* [ ] Confirm whether this is implementation, review, or stabilization

## File Conflict Check

* [ ] Check whether any relevant lock exists under `.ai/locks`
* [ ] Do not edit files locked by another active agent
* [ ] If creating a lock, include owner, task, files, and expected release
* [ ] Remove or update lock when finished

## Design Check

* [ ] Follow existing architecture
* [ ] Do not introduce new abstractions if an existing one fits
* [ ] Do not overload an existing abstraction with unrelated responsibility
* [ ] Keep changes small and focused
* [ ] Preserve behavior unless explicitly asked to change it
* [ ] Avoid database schema changes unless explicitly approved
* [ ] Avoid frontend changes unless explicitly included in scope

## Implementation Check

* [ ] Keep naming consistent with existing code
* [ ] Keep namespaces consistent with folder structure
* [ ] Register new services in DI when needed
* [ ] Avoid duplicate business logic
* [ ] Avoid hardcoded workflow behavior where configuration is expected
* [ ] Preserve existing fallback behavior
* [ ] Avoid touching unrelated files
* [ ] Remove unused code when safely replaced
* [ ] Avoid editing generated docs

## Test Check

* [ ] Add focused tests for new or changed behavior
* [ ] Add regression tests for bug fixes
* [ ] Add drift-detection tests when consolidating maps/catalogs
* [ ] Update existing tests only when necessary
* [ ] Prefer narrow unit tests for pure logic
* [ ] Prefer integration tests for API, persistence, or DI behavior
* [ ] Do not claim tests passed unless they were actually run

## Required Local Validation

Run when available:

```bash
dotnet build wfmgr.sln -c Debug
dotnet test Wfmgr.Api.Tests/Wfmgr.Api.Tests.csproj
```

If `dotnet` is unavailable, report:

```text
The .NET SDK is not available in this environment. Build/tests were not run.
```

Then provide Daniel the exact local commands.

## Handoff Check

Before finishing:

* [ ] Summarize files changed
* [ ] Summarize behavior preservation
* [ ] Summarize tests added or updated
* [ ] Summarize tests run
* [ ] List risks or notes
* [ ] Recommend the next agent or next step
* [ ] Update `.ai/handoffs/current-handoff.md` if meaningful work was completed

## Stop Conditions

Stop and ask for review if:

* [ ] A database schema change is needed
* [ ] An EF migration is needed
* [ ] A deployment command is needed
* [ ] A secret/configuration change is needed
* [ ] The change expands beyond the task scope
* [ ] Existing behavior cannot be preserved
* [ ] Tests fail for unclear reasons
* [ ] The repository state has conflicting edits
* [ ] An active lock exists for the files you need to edit


# File: .ai/agents/backend-developer/REVIEW_TEMPLATE.md

# Backend Review Report

## Summary

Briefly summarize what was reviewed.

## Review Scope

State what files and behavior were reviewed.

## Files Reviewed

| File | Assessment |
| ---- | ---------- |
|      |            |

## Design Review

Assess whether the implementation fits existing architecture.

## Behavior Preservation

Assess whether existing behavior is preserved.

## Test Coverage Assessment

Assess whether tests are sufficient.

## Build / Test Status

State whether build/tests were run.

```bash
dotnet build wfmgr.sln -c Debug
dotnet test Wfmgr.Api.Tests/Wfmgr.Api.Tests.csproj
```

Result:

```text
Not run / Passed / Failed
```

## Issues Found

List any issues.

Use this format:

```markdown
### Issue N

- Area:
- File:
- Problem:
- Risk:
- Recommendation:
- Priority:
```

## Risks / Notes

List remaining risks or uncertainty.

## Recommendation

Choose one:

```text
Approve
Approve after local build/test
Request changes
Escalate to Architect
Escalate to Quality Engineer
```

## Next Step

State the next concrete action.
