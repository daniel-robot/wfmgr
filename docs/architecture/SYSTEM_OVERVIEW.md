# System Overview

## Purpose

wfmgr is a configurable workflow management system for radiotherapy workflow orchestration.

The system manages cases, work items, workflow profiles, workflow rules, and external events.

## Architecture Overview

```text
Frontend: Angular 21
        |
        v
Backend: ASP.NET Core Web API
        |
        v
Application Layer
        |
        v
Domain Layer
        |
        v
Infrastructure Layer
        |
        v
PostgreSQL
```

## Main Components

### Wfmgr.Api
Hosts HTTP APIs, controllers, authentication/authorization integration, and API configuration.

### Wfmgr.Application

Contains workflow application services, transition logic, validation logic, DTO orchestration, and use case implementation.

### Wfmgr.Domain

Contains domain entities, enums, domain constants, and domain-level concepts.

### Wfmgr.Infrastructure

Contains persistence, EF Core integration, database access, external adapters, and infrastructure services.

### Wfmgr.Contracts

Contains shared contracts and DTOs.

### Wfmgr.Engine

Contains workflow execution or orchestration logic.

### Wfmgr.Api.Tests

Contains backend API and integration tests.

### wfmgr-ui

Angular frontend application.

## Configurability Goal

Workflow behavior should be configurable by workflow profiles and workflow rules.

Expected configurable areas:

- Stages
- Statuses
- Transitions
- Work item types
- Role assignment
- SLA
- Validation
- Compensation
- External event mapping

## AI Agent Integration Goal

AI agents should assist with repository inspection, workflow configuration review, test suggestions, documentation, and security review.

AI agents must operate with clear permission boundaries and human approval for risky operations.