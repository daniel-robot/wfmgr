# Architect Checklist

## General Architecture Review

- [ ] Does the change follow existing project patterns?
- [ ] Is the change small and incremental?
- [ ] Are module boundaries clear?
- [ ] Are responsibilities placed in the right layer?
- [ ] Does the change avoid unnecessary abstraction?
- [ ] Is the design easy to test?
- [ ] Is the design easy to roll back?
- [ ] Are risks documented?
- [ ] Are assumptions documented?
- [ ] Is an ADR needed?

## API Boundary Review

- [ ] Are request and response DTOs clear?
- [ ] Is domain logic kept out of controllers/UI?
- [ ] Are validation responsibilities clear?
- [ ] Are errors handled consistently?
- [ ] Is backward compatibility considered?
- [ ] Is versioning needed?
- [ ] Are security and authorization boundaries clear?

## Data Model Review

- [ ] Is entity ownership clear?
- [ ] Are relationships appropriate?
- [ ] Are migration impacts understood?
- [ ] Are indexes or constraints needed?
- [ ] Is concurrency considered?
- [ ] Is audit/history needed?
- [ ] Is tenant or organization isolation needed?

## Frontend Architecture Review

- [ ] Are components placed in the right module/page area?
- [ ] Is state management consistent with existing patterns?
- [ ] Are API contracts respected?
- [ ] Is form validation consistent?
- [ ] Are error/loading states considered?

## DevOps / Deployment Review

- [ ] Are environment variables documented?
- [ ] Are local and Docker paths clear?
- [ ] Are secrets avoided in source control?
- [ ] Is observability/logging impacted?
- [ ] Is deployment or rollback impacted?

## Testing Impact

- [ ] Are unit tests needed?
- [ ] Are integration tests needed?
- [ ] Are frontend tests needed?
- [ ] Are regression cases identified?
- [ ] Are edge cases documented?
