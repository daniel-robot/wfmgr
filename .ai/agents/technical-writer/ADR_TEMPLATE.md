# ADR-NNN: Title

## Status

Choose one:

```text
Proposed
Accepted
Superseded
Deprecated
```

## Date

YYYY-MM-DD

## Context

Describe the situation that led to the decision.

Include:

* The problem
* The relevant existing design
* The pain point or risk
* The constraints

Keep this section factual.

## Decision

State the decision clearly.

Example:

```text
We will introduce IOutboxRouteProvider for outbox route mapping and keep IOutboxRoutingPolicy focused on delivery mode selection.
```

## Alternatives Considered

### Option 1: Option name

Pros:

*

Cons:

*

### Option 2: Option name

Pros:

*

Cons:

*

## Consequences

List the expected consequences.

Include:

* Positive consequences
* Trade-offs
* Follow-up work
* Testing implications
* Documentation implications

## Validation

State how the decision was validated.

Examples:

* Build command
* Test command
* Review result
* Manual inspection
* Not yet validated

## Related Files

List relevant files.

Examples:

```text
Wfmgr.Application/Workflows/V1/Outbox/OutboxRouteProvider.cs
Wfmgr.Application/Workflows/V1/SideEffects/WorkflowSideEffectService.cs
Wfmgr.Application/Workflows/V1/Compensation/WorkflowCompensationService.cs
Wfmgr.Api.Tests/OutboxRouteProviderTests.cs
```
