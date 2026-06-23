Decision:
Create IOutboxRouteProvider for route mapping.
Keep IOutboxRoutingPolicy for delivery mode / transport policy.

Reason:
They are separate concerns.

Consequence:
WorkflowSideEffectService and WorkflowCompensationService now share route definitions.
OutboxRouteProviderTests detect route drift.