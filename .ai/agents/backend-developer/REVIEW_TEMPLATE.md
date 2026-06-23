# Backend Review Report

## Summary

Briefly summarize what was reviewed and the overall result.

Example:

```text
Reviewed the current backend implementation for scope control, design fit, behavior preservation, test coverage, and build/test readiness.
```

## Review Type

Select one:

```text
Implementation review
Stabilization review
Bug fix review
Refactor review
Test review
Regression review
```

## Review Scope

Describe the requested review scope.

Include:

* Task or handoff reviewed
* Files reviewed
* Behavior reviewed
* Tests reviewed
* Areas intentionally not reviewed

## Required Context Read

Confirm the context files reviewed:

* [ ] `.ai/project-context.md`
* [ ] `.ai/agent-rules.md`
* [ ] `.ai/working-agreement.md`
* [ ] `.ai/handoffs/current-handoff.md`
* [ ] Relevant `.ai/tasks/*.md` task file
* [ ] Relevant ADR under `docs/architecture/decisions/`, if applicable

## Repository State

```text
Branch:
Working tree status:
Active locks:
Review mode:
```

Example:

```text
Branch: ai
Working tree status: uncommitted backend changes present
Active locks: none
Review mode: read-only review
```

## Files Reviewed

| File | Assessment |
| ---- | ---------- |
|      |            |

## Design Review

Assess whether the implementation fits the existing architecture.

Cover:

* Whether the change follows existing patterns
* Whether new abstractions are justified
* Whether existing abstractions were reused appropriately
* Whether responsibilities remain separated
* Whether the change avoids unnecessary redesign

## Scope Control

Confirm whether the implementation stayed within scope.

| Check                         | Result |
| ----------------------------- | ------ |
| No unrelated frontend changes |        |
| No database schema changes    |        |
| No EF migrations              |        |
| No deployment changes         |        |
| No secret/config changes      |        |
| No generated docs edited      |        |
| No broad redesign             |        |

## Behavior Preservation

Assess whether existing behavior is preserved.

Include:

* Old behavior
* New behavior
* Whether behavior changed intentionally or unintentionally
* Fallback behavior
* Error handling
* Edge cases

| Behavior Area | Old Behavior | New Behavior | Verdict |
| ------------- | ------------ | ------------ | ------- |
|               |              |              |         |

## Test Coverage Assessment

Assess whether tests are sufficient.

Cover:

* New tests added
* Existing tests affected
* Regression coverage
* Edge case coverage
* Missing test cases
* Whether tests are focused or too broad

| Test File | Test / Area | Assessment |
| --------- | ----------- | ---------- |
|           |             |            |

## Build / Test Status

State whether build and tests were run.

Expected commands:

```bash
dotnet build wfmgr.sln -c Debug
dotnet test Wfmgr.Api.Tests/Wfmgr.Api.Tests.csproj
```

Result:

```text
Not run / Passed / Failed
```

If not run, explain why.

Example:

```text
The .NET SDK is not available in this environment. Build/tests were not run.
Daniel should run the commands locally on macOS.
```

## Issues Found

List issues found during review.

If no issues were found, write:

```text
No blocking issues found.
```

Use this format for each issue:

```markdown
### Issue N

- Area:
- File:
- Problem:
- Risk:
- Recommendation:
- Priority: High / Medium / Low
```

## Risks / Notes

List remaining risks, assumptions, or follow-up notes.

Examples:

* Build/tests could not be run in the current environment.
* A test uses a heuristic that may need future maintenance.
* The implementation is correct but should be documented in an ADR.
* Local validation is required before commit.

## Recommendation

Choose one:

```text
Approve
Approve after local build/test
Request changes
Escalate to Architect
Escalate to Quality Engineer
Escalate to Daniel
```

## Commit Readiness

Choose one:

```text
Ready to commit
Ready to commit after local build/test
Not ready to commit
Needs design review
Needs test fixes
```

## Recommended Next Step

State the next concrete action.

Examples:

```text
Run local build and backend tests on macOS.
```

```text
Send the implementation to Quality Engineer for final validation.
```

```text
Create an ADR documenting the design decision.
```

## Final Review Summary

Use this format:

```markdown
## Summary

## Validation Result

## Files Reviewed

## Design Review

## Behavior Preservation

## Test Coverage Assessment

## Tests Run

## Risks / Notes

## Recommendation

## Next Step
```
