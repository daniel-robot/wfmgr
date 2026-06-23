# Six-Month Execution Plan

## Purpose

This file tracks Daniel's six-month execution plan for building a practical portfolio around:

```text
Secure AI Agent + Azure + Enterprise Workflow
```

The goal is not to study many topics passively, but to produce a working, demonstrable, and documented capability set that can support:

* Career growth
* Internal influence
* Interview preparation
* Portfolio building
* Future consulting or side-business opportunities

## Six-Month Goal

Within six months, build a demonstrable and well-documented platform showing how AI agents can be safely integrated into enterprise workflow software.

Target positioning:

```text
Secure AI Agent Platform for Enterprise Workflow
```

Personal positioning:

```text
Daniel is not only a .NET/Azure developer. He can safely integrate AI agents into enterprise software engineering and regulated workflow systems.
```

## Monthly Roadmap

| Month   | Theme                            | Core Deliverable                                                             |
| ------- | -------------------------------- | ---------------------------------------------------------------------------- |
| Month 1 | Foundation and first closed loop | Project context, agent rules, repository inspection, first safe refactor     |
| Month 2 | AI Agent engineering workflow    | Repeatable prompts, workflow config review, test suggestion workflow         |
| Month 3 | Security and compliance          | Agent permission model, human approval model, audit logging, threat model    |
| Month 4 | Azure and enterprise deployment  | Azure architecture, deployment runbooks, observability, cost estimate        |
| Month 5 | Portfolio packaging              | English README, demo scenarios, architecture overview, security overview     |
| Month 6 | Career and business conversion   | Internal sharing, resume story, LinkedIn profile, consulting service outline |

---

# Month 1: Foundation and First Closed Loop

## Goal

Complete one safe AI-assisted engineering workflow:

```text
Architect inspection
→ Quality Engineer review
→ Backend Developer implementation
→ Quality Engineer validation
→ Documentation / ADR
```

The first Month 1 focus is:

```text
Workflow hardcoding inspection and first safe backend refactor.
```

## Deliverables

* `.ai/project-context.md`
* `.ai/agent-rules.md`
* `.ai/tasks/inspect-workflow-hardcoding.md`
* `prompts/repository-inspector.md`
* `docs/agent/examples/hardcode-inspection-report.md`
* First backend refactor candidate implemented and validated
* First ADR created under `docs/architecture/decisions/`

## Current Month 1 Status

### Completed

* `.ai` folder structure created
* Agent roles created
* Agent coordination files created
* Architect agent completed workflow hardcoding inspection
* Hardcoding inspection identified outbox routing duplication
* Quality Engineer reviewed the testability of outbox routing consolidation
* Backend Developer reviewed the implementation direction
* `IOutboxRouteProvider` design was validated as separate from `IOutboxRoutingPolicy`

### In Progress

Current implementation candidate:

```text
Consolidate outbox routing into IOutboxRouteProvider
```

Design decision:

```text
IOutboxRoutingPolicy = delivery mode / transport policy
IOutboxRouteProvider = target system + outbox action + optional message type route mapping
```

### Pending

* Daniel runs local build
* Daniel runs backend tests
* Quality Engineer performs final validation
* ADR is created
* Refactor is committed as a focused backend change

## Month 1 Validation Commands

Run locally on macOS:

```bash
cd /Users/daniel/Projects/wfmgr

dotnet build wfmgr.sln -c Debug
dotnet test Wfmgr.Api.Tests/Wfmgr.Api.Tests.csproj
```

## Month 1 Success Criteria

Month 1 is successful when:

* The repository has clear agent context and rules
* The Architect agent can inspect workflow hardcoding
* The inspection report is saved and reviewable
* At least one finding becomes a safe, tested backend improvement
* The improvement has build/test validation
* The design decision is documented in an ADR
* The handoff process is proven through at least one full loop

## Month 1 Next Steps

1. Run local build and backend tests.
2. If tests pass, ask Quality Engineer for final validation.
3. Create ADR:

```text
docs/architecture/decisions/ADR-001-consolidate-outbox-routing.md
```

4. Commit the focused refactor.
5. Update `.ai/handoffs/current-handoff.md`.

---

# Month 2: AI Agent Engineering Workflow

## Goal

Make the agent system repeatable, not ad hoc.

Agents should be able to run fixed tasks with stable prompts, expected output formats, and reviewable reports.

## Target Capabilities

| Agent Capability         | Purpose                                                    |
| ------------------------ | ---------------------------------------------------------- |
| Repository Inspector     | Analyze code structure, hardcoding, and architecture risks |
| Workflow Config Reviewer | Review workflow profile and workflow rule configuration    |
| Test Suggestion Agent    | Suggest API, unit, and frontend tests based on findings    |

## Planned Files

```text
prompts/repository-inspector.md
prompts/workflow-config-reviewer.md
prompts/test-suggestion-agent.md
docs/agent/examples/hardcode-inspection-report.md
docs/agent/examples/config-review-report.md
docs/agent/examples/test-suggestion-report.md
.ai/tasks/review-workflow-config.md
.ai/tasks/suggest-tests-for-workflow-change.md
```

## Success Criteria

Month 2 is successful when:

* Three stable prompts exist
* Three example reports exist
* Agent output formats are consistent
* The handoff process works across Architect, Quality Engineer, Backend Developer, and Technical Writer
* At least one workflow config review can be demonstrated end-to-end

---

# Month 3: Security and Compliance

## Goal

Upgrade the project from an AI demo to a secure enterprise AI agent workflow.

## Planned Files

```text
docs/security/AGENT_SECURITY_MODEL.md
docs/security/THREAT_MODEL.md
docs/security/HUMAN_IN_THE_LOOP.md
docs/security/AUDIT_LOGGING.md
```

## Security Model Topics

* Agent permission levels
* Human approval
* Audit logging
* Prompt injection risk
* Data leakage risk
* Secret exposure risk
* Unsafe command execution risk
* Misleading AI recommendation risk
* Over-trust of AI output

## Permission Levels

| Level | Type                                                        | Approval                   |
| ----- | ----------------------------------------------------------- | -------------------------- |
| L0    | Read source code and documentation                          | Not required               |
| L1    | Generate findings and recommendations                       | Not required               |
| L2    | Modify code, tests, docs, or config                         | Required                   |
| L3    | Deploy, delete, change secrets, or affect external services | Explicit approval required |

## Success Criteria

Month 3 is successful when:

* Agent permission model is documented
* Human-in-the-loop model is documented
* Agent audit logging design is documented
* Threat model is documented
* The project can be described as a secure AI agent platform

---

# Month 4: Azure and Enterprise Deployment

## Goal

Create an enterprise-style deployment and operations story.

## Planned Files

```text
docs/architecture/AZURE_ARCHITECTURE.md
docs/runbooks/AZURE_DEPLOYMENT.md
docs/runbooks/OBSERVABILITY.md
docs/runbooks/INCIDENT_RESPONSE.md
docs/runbooks/BACKUP_AND_RESTORE.md
```

## Target Architecture

Initial preferred deployment:

```text
Azure VM + Docker Compose
```

Future enterprise option:

```text
Azure Container Apps + Managed Identity + Key Vault
```

## Observability Areas

* API health check
* Agent gateway health check
* PostgreSQL health check
* Failed agent runs
* High-risk action attempts
* Approval events
* Error rate
* Latency
* Cost tracking

## Success Criteria

Month 4 is successful when:

* Azure deployment document exists
* Observability plan exists
* Incident response runbook exists
* Backup and restore runbook exists
* Cost estimate exists
* One deployable demo path is documented

---

# Month 5: Portfolio Packaging

## Goal

Package the project so it can be shown to managers, interviewers, and future clients.

## Planned Files

```text
README.md
docs/architecture/ARCHITECTURE_OVERVIEW.md
docs/security/SECURITY_OVERVIEW.md
docs/demo/DEMO_SCRIPT_1_REPOSITORY_INSPECTION.md
docs/demo/DEMO_SCRIPT_2_WORKFLOW_CONFIG_REVIEW.md
docs/demo/DEMO_SCRIPT_3_HUMAN_IN_THE_LOOP.md
```

## Demo Scenarios

### Demo 1: Repository Inspection

Agent scans workflow code and identifies hardcoded workflow behavior.

### Demo 2: Workflow Config Review

Agent reviews workflow profile/rule configuration and identifies risks.

### Demo 3: Human-in-the-loop Safety

Agent attempts or proposes a risky operation and the system requires human approval.

## Success Criteria

Month 5 is successful when:

* English README exists
* Architecture overview exists
* Security overview exists
* Three demo scripts exist
* The project can be explained in 3-5 minutes

---

# Month 6: Career and Business Conversion

## Goal

Convert the project into practical career value.

## Tracks

### Track A: Internal Influence

Prepare an internal sharing:

```text
How to Safely Use AI Agents in Enterprise Software Engineering
```

Suggested outline:

1. Why AI agents are different from chatbots
2. Risks of using AI agents in enterprise systems
3. Permission model for AI agents
4. Human-in-the-loop approval
5. Audit logging
6. Example: workflow configuration review
7. Suggested next step for the team

### Track B: Interview and Resume

Target roles:

| Direction           | Target Roles                                                       |
| ------------------- | ------------------------------------------------------------------ |
| AI Engineering      | AI Platform Engineer / AI Application Architect                    |
| Cloud + Security    | Cloud Security Architect / DevSecOps Lead                          |
| Enterprise Software | Principal Software Engineer / Staff Engineer / Engineering Manager |

Resume bullet:

```text
Built a secure AI agent platform for enterprise workflow management, integrating repository-aware analysis, configurable workflow review, human-in-the-loop approval, audit logging, threat modeling, and Azure deployment.
```

### Track C: Consulting / Side Business

Service idea:

```text
Enterprise AI Agent Safety Assessment
```

Service scope:

1. Assess current AI coding tool usage
2. Identify data leakage, permission, audit, and compliance risks
3. Design agent usage rules
4. Set up a private agent gateway
5. Connect code repositories, documentation, and CI/CD
6. Provide Azure deployment and hardening recommendations

## Success Criteria

Month 6 is successful when:

* Internal sharing material exists
* Resume project description exists
* LinkedIn summary exists
* Consulting service one-pager exists
* Complete demo project exists
* Reusable agent safety standard exists

---

# Weekly Execution Rhythm

Recommended weekly investment:

```text
6-8 hours per week
```

Suggested rhythm:

| Time                 | Activity                                      |
| -------------------- | --------------------------------------------- |
| Two weekday evenings | 1.5 hours each for technical implementation   |
| Weekend half-day     | 3-4 hours for documentation, demo, and review |

Weekly rule:

```text
Every week should produce one concrete artifact.
```

Artifacts can be:

* A task file
* A prompt
* A report
* A small refactor
* A test
* An ADR
* A runbook
* A demo script

---

# 24-Week Plan

| Weeks | Focus                                 | Output                                                                         |
| ----- | ------------------------------------- | ------------------------------------------------------------------------------ |
| 1-2   | Project positioning and agent context | `.ai/project-context.md`, `.ai/agent-rules.md`, first task file                |
| 3-4   | Hardcode Inspector                    | `prompts/repository-inspector.md`, hardcoding report, first refactor candidate |
| 5-6   | Workflow Config Reviewer              | config reviewer prompt and example report                                      |
| 7-8   | Test Suggestion Agent                 | test suggestion prompt and example report                                      |
| 9-10  | Permission model                      | agent security model and human approval docs                                   |
| 11-12 | Audit logging and threat model        | audit logging design and threat model                                          |
| 13-14 | Azure architecture                    | Azure architecture doc and deployment runbook                                  |
| 15-16 | Observability and incident response   | observability and incident response runbooks                                   |
| 17-18 | Demo 1 and Demo 2                     | repository inspection demo and workflow config review demo                     |
| 19-20 | Demo 3 and security story             | human-in-the-loop demo and security overview                                   |
| 21-22 | Portfolio packaging                   | English README, architecture overview, security overview                       |
| 23-24 | Career conversion                     | internal sharing, resume bullet, LinkedIn summary, consulting one-pager        |

---

# Priority Outcomes

If time is limited, prioritize these three outcomes:

## 1. Secure AI Agent Engineering Workflow

A repeatable workflow showing:

```text
Inspect → Review → Implement → Validate → Document
```

## 2. Agent Security Model

A clear model for:

```text
Permissions → Approval → Audit → Risk control
```

## 3. English README and Demo

A clear portfolio story that can be shown externally.

---

# Current Operating Rule

For every meaningful task, use this handoff flow:

```text
Product Owner / Project Manager
→ Architect
→ Quality Engineer
→ Backend or Frontend Developer
→ Quality Engineer
→ Technical Writer
→ Project Manager
```

Not every task needs every role.

Default rule:

```text
Use the minimum number of agents needed to complete a safe, reviewable artifact.
```

## Role Boundaries

| Agent              | Should Do                            | Should Not Do                     |
| ------------------ | ------------------------------------ | --------------------------------- |
| Product Owner      | Define value and acceptance criteria | Implement code                    |
| Project Manager    | Track progress and handoff           | Redesign architecture             |
| Architect          | Analyze design and create ADRs       | Implement routine code            |
| Quality Engineer   | Define tests and validate changes    | Modify production code            |
| Backend Developer  | Implement backend changes            | Redesign scope without approval   |
| Frontend Developer | Implement frontend changes           | Change backend contracts casually |
| DevOps Engineer    | Review deployment and infra          | Change app logic                  |
| Technical Writer   | Update docs and demo scripts         | Change code behavior              |

---

# Current Next Step

As of the current Month 1 work, the next step is:

```text
Daniel runs local build and backend tests for the OutboxRouteProvider refactor.
```

Commands:

```bash
cd /Users/daniel/Projects/wfmgr

dotnet build wfmgr.sln -c Debug
dotnet test Wfmgr.Api.Tests/Wfmgr.Api.Tests.csproj
```

If tests pass:

```text
Ask Quality Engineer for final validation.
Create ADR-001.
Commit focused backend refactor.
```

If tests fail:

```text
Send the failure output to Backend Developer.
Fix only issues related to outbox routing consolidation.
```
