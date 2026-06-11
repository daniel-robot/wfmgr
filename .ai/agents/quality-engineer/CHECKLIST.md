# Quality Engineer Checklist

## Acceptance Criteria Review

- [ ] Are acceptance criteria testable?
- [ ] Are expected outcomes clear?
- [ ] Are happy paths covered?
- [ ] Are negative paths covered?
- [ ] Are validation rules covered?
- [ ] Are permission rules covered?
- [ ] Are edge cases covered?
- [ ] Are error states covered?
- [ ] Are dependencies clear?

## Test Case Coverage

- [ ] Happy path
- [ ] Negative path
- [ ] Edge cases
- [ ] Regression cases
- [ ] Permission cases
- [ ] Data validation
- [ ] State transitions
- [ ] External integration failures
- [ ] Concurrency or conflict cases
- [ ] Cancellation or partial completion

## Regression Risk Review

- [ ] Existing API behavior impacted?
- [ ] Existing UI behavior impacted?
- [ ] Existing domain logic impacted?
- [ ] Existing database schema impacted?
- [ ] Existing configuration impacted?
- [ ] Existing deployment behavior impacted?
- [ ] Existing tests impacted?
- [ ] Documentation or setup instructions impacted?

## Test Automation Review

- [ ] Unit test useful?
- [ ] Integration test useful?
- [ ] API test useful?
- [ ] Frontend component test useful?
- [ ] End-to-end test useful?
- [ ] Smoke test useful?
- [ ] Regression test useful?
- [ ] Manual-only validation acceptable?

## Release Readiness

- [ ] Critical paths validated?
- [ ] Known risks documented?
- [ ] Validation commands run or recommended?
- [ ] Bugs/blockers documented?
- [ ] Handoff updated?
