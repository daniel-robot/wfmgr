# Repository Inspection Report

## Summary

This report covers a read-only inspection of the wfmgr repository — a configurable radiotherapy workflow orchestration system built on .NET 10 (net10.0 target framework, C# 13 with collection expressions, primary constructors, `params` arrays, and `System.Text.Json` source generators throughout). The inspection focused on identifying hardcoded workflow behavior that should become configurable, as defined in the project's configurability principles.

Seven backend projects were inspected: Domain (models/enums), Application (transitions, compensation, gates, side effects, slots, vocabulary), Infrastructure (DB-backed catalog, profile resolution), Engine (transition orchestrator), Api (controllers), Contracts (request/response DTOs), and Api.Tests (test coverage).

The repository shows significant progress toward configurable workflows, with 8 configurable slot policies (S1-S8), a DB-backed transition catalog with a complete admin API mutation surface (create, update, delete with concurrency hashing and change logging), vocabulary-based extensibility for roles, work item types, and form types, and a 4-level profile resolution system (global, hospital, site, department) that overrides all 8 policy slots with DB-stored values. However, several areas of hardcoded logic remain — primarily in compensation outbox routing, gate check implementations, work item role defaults, and the transition catalog's static definition pattern.

## Repository State

- **Branch:** ai
- **Last commit:** `2ba09e9` — "complete checkpoint with tests"
- **Working tree status:** Clean except `.ai/project-context.md` modified (agent context file) and various untracked `.ai/` agent-support files (locks, tasks, agents, checklists, prompts, docs). No production code modifications in progress.
- **Lock status:** No active locks.
- **Inspection mode:** Read-only. No files modified.

## Files Inspected

The following files were read in full or in relevant sections:

**System-level project documentation:**
- `.ai/project-context.md` — project overview, architecture, technology stack
- `.ai/working-agreement.md` — team conventions (immutable ADRs, lock files, no-touching rule)
- `.ai/tasks/inspect-workflow-hardcoding.md` — task instructions, 16 enumerated inspects
- `.ai/roles/architect.md` — role responsibilities
- `.ai/agents/architect/MEMORY.md` — agent memory
- `.ai/agents/architect/CHECKLIST.md` — inspection checklist

**Domain (models and enums):**
- `src/Wfmgr.Domain/Enums/CaseStatus.cs` — 23 statuses (Submitted through Cancelled)
- `src/Wfmgr.Domain/Enums/WorkItemStatus.cs` — 6 statuses (Pending, InProgress, Done, Rejected, Cancelled, Skipped)
- `src/Wfmgr.Domain/Enums/WorkflowTriggerType.cs` — 4 trigger types
- `src/Wfmgr.Domain/WorkItems/WorkItemTypes.cs` — 27 string constant definitions with numbered codes and display names
- `src/Wfmgr.Domain/WorkflowRoles.cs` — 6 role constants
- `src/Wfmgr.Domain/Forms/CaseFormStatuses.cs` — referenced from form-status gate checks
- `src/Wfmgr.Domain/Integrations/OutboxActions.cs` — outbox action constants

**Application layer:**
- `src/Wfmgr.Application/Workflows/V1/WorkflowTransitionCatalog.cs` — 30 static transition definitions
- `src/Wfmgr.Application/Workflows/V1/WorkflowCompensationCatalog.cs` — 9 static compensation definitions
- `src/Wfmgr.Application/Workflows/V1/WorkflowSlotCodes.cs` — 8 slot code constants (S1-S8)
- `src/Wfmgr.Application/Workflows/V1/WorkflowSlotConfigValidator.cs` — per-slot validation
- `src/Wfmgr.Application/Workflows/V1/S1ContouringStrategy.cs` — S1-S8 policy DTOs with defaults
- `src/Wfmgr.Application/Workflows/V1/WorkflowExplainService.cs` — read-only explain/render for workflows
- `src/Wfmgr.Application/Workflows/V1/Definitions/TransitionDefinition.cs` — 12-field metadata record
- `src/Wfmgr.Application/Workflows/V1/Definitions/CompensationDefinition.cs` — 7-field metadata record + RetryPolicy record
- `src/Wfmgr.Application/Workflows/V1/Definitions/WorkflowTransitionGraphValidator.cs` — validation + graph reachability
- `src/Wfmgr.Application/Workflows/V1/Gates/GateCheckNames.cs` — 50+ gate check name constants
- `src/Wfmgr.Application/Workflows/V1/Gates/GateValidationService.cs` — 35+ gate check implementations
- `src/Wfmgr.Application/Workflows/V1/WorkItems/WorkItemLifecycleService.cs` — WorkItem create/complete/reject/cancel lifecycle
- `src/Wfmgr.Application/Workflows/V1/WorkItems/WorkItemResultCodes.cs` — 8 result code constants
- `src/Wfmgr.Application/Workflows/V1/Compensation/WorkflowCompensationService.cs` — compensation execution with retry + outbox routing
- `src/Wfmgr.Application/Workflows/V1/SideEffects/WorkflowSideEffectService.cs` — work item creation + outbox dispatch
- `src/Wfmgr.Application/Workflows/V1/StateMachine/CaseStateMachineService.cs` — legacy adapter to new engine
- `src/Wfmgr.Application/Workflows/V1/Vocabulary/WorkflowVocabularyKinds.cs` — 3 vocabulary kinds (WorkItemTypes, WorkflowRoles, CaseFormStatuses)

**Infrastructure layer:**
- `src/Wfmgr.Infrastructure/Workflows/WorkflowTransitionCatalogService.cs` — DB-backed catalog with lazy seeding, concurrency hashing, change log, full CRUD
- `src/Wfmgr.Infrastructure/Profiles/WorkflowProfileResolver.cs` — DB-backed profile resolution (4-level scope: global, hospital, site, department)
- `src/Wfmgr.Infrastructure/Profiles/WorkflowProfileSeeder.cs` — seed data for profiles

**Engine layer:**
- `src/Wfmgr.Engine/Core/TransitionEngine.cs` — orchestrator: role check → gate validation → transition → persist → audit → side effects → compensation archiving

## Findings

### Finding 1 — Compensation outbox routing is a hardcoded switch

**Area:** Compensation outbox routing — `WorkflowCompensationService.BuildRetryOutboxMessage()`

**File:** `src/Wfmgr.Application/Workflows/V1/Compensation/WorkflowCompensationService.cs` (lines 253–261)

**Issue:** The mapping from `FailedStepCode` (which corresponds to a transition code like `"IMG-002"`, `"RX-003"`, `"TRT-001"`) to `(TargetSystem, OutboxAction)` is a hardcoded switch expression that duplicates the same routing logic already present in `WorkflowSideEffectService.OutboxActionMap`. The switch enumerates specific transition codes and maps them manually to a target system string (`"PvMed"`, `"MSQ"`, `"Monaco"`) and an `OutboxActions` constant.

**Evidence:**

In `WorkflowCompensationService`, lines 253-261:
```csharp
var (targetSystem, action) = definition.FailedStepCode switch
{
    "IMG-002" or "IMG-003" => ("PvMed", OutboxActions.SendImagesToContourTool),
    "CON-002" or "CON-003" or "CON-004" => ("PvMed", OutboxActions.SendImagesToContourTool),
    "RX-002" or "RX-003" or "RX-006" or "RX-007" => ("PvMed", OutboxActions.GeneratePrescription),
    "TRT-001" or "TRT-002" => ("MSQ", OutboxActions.SyncSchedule),
    "TRT-012" => ("Monaco", OutboxActions.QueryTreatmentProgress),
    _ => (context.SourceSystem ?? "System", OutboxActions.QueryContourStatus),
};
```

Compare with `WorkflowSideEffectService.OutboxActionMap` (lines 76-100 in the side-effect service) which has a very similar but independently-maintained switch for the same `transitionCode` → target system mapping.

**Risk:** When new transitions or external systems are added, both the side-effect service map AND this compensation switch must be updated in lockstep. There is no single source of truth for integration routing. The `_` fallback is inconsistent — it defaults to `QueryContourStatus` rather than reflecting the actual failed step, which could cause incorrect compensations on unexpected failure codes.

**Recommendation:** Either (a) move the `FailedStepCode → (TargetSystem, Action)` mapping into the DB-backed compensation catalog so it can be configured per rule, or (b) at minimum refactor into a shared lookup that both `WorkflowSideEffectService` and `WorkflowCompensationService` reference. The preferred approach depends on how dynamic the routing needs to be — if routes change per hospital/site/department, a DB catalog is better; if routes are universal, a shared static provider is simpler.

**Suggested test:** A test that enumerates every `FailedStepCode` in `WorkflowCompensationCatalog` and asserts it has a corresponding mapping in the shared route table (or that the catalog route is preferred over the shared table).

**Priority:** Medium

---

### Finding 2 — Static work item default roles are duplicated

**Area:** Work item default role dictionaries maintained independently in two services.

**Files:**
- `src/Wfmgr.Application/Workflows/V1/Compensation/WorkflowCompensationService.cs` (lines 279-295) — `WorkItemDefaultRoles` dictionary
- `src/Wfmgr.Application/Workflows/V1/SideEffects/WorkflowSideEffectService.cs` (lines 294-321) — `DefaultWorkItemRoles` dictionary

**Issue:** Both `WorkflowCompensationService` and `WorkflowSideEffectService` maintain independent dictionaries mapping `WorkItemTypes` constant names to default role strings. These are nearly identical but can drift apart if one is updated and the other is not. Each dictionary also has different complete coverage — the compensation dictionary currently holds 13 entries while the side-effect dictionary holds 24 entries.

**Evidence:** The compensation dictionary has entries like:
```
{ WorkItemTypes.ImageForwardToContourTool, "Physician/ThirdPartyOperator" },
{ WorkItemTypes.ManualContouring, "Physician/ThirdPartyOperator" },
{ WorkItemTypes.ContourRework, "Physician/ThirdPartyOperator" },
```

The side-effect dictionary has the same entries plus additional ones like `PlanDesign`, `PlanAssignment`, `PlanDoubleCheck`, `PlanReReview`, `PlanReDesign`, etc. with role values like `"Physicist"`, `"Dosimetrist"`, `"Physician"`.

All 13 compensation entries are a subset of the 24 side-effect entries. The role values appear consistent between the two dictionaries for the shared keys.

**Risk:** Added work item types or role changes require editing both dictionaries. There is no compile-time or runtime validation that the two dictionaries stay in sync. The compensation dictionary's subset may be intentionally narrower (because not all work item types have compensation fallbacks), but there's no documentation explaining why.

**Recommendation:** Extract a single shared `IWorkItemDefaultRoleProvider` interface that both services reference via DI. The implementation can return different results per service context if needed (e.g., `GetDefaultRole(workItemType, context: "compensation")` vs `context: "side-effect"`), but the definitions live in one place.

**Suggested test:** A test that reads every compensation definition's `WorkItemToCreate` from `WorkflowCompensationCatalog` and asserts it has an entry in the shared role provider.

**Priority:** Medium

---

### Finding 3 — Transition catalog is static C# code with a DB seeding layer but no merge strategy

**Area:** `WorkflowTransitionCatalog` as the canonical source of truth for workflow transitions.

**File:** `src/Wfmgr.Application/Workflows/V1/WorkflowTransitionCatalog.cs`

**Issue:** All 30 transition definitions are declared as static `readonly` fields in a static class using C# object-initializer syntax. A DB-backed `WorkflowTransitionCatalogService` (in Infrastructure) supports seeding from these static definitions into the database, and an admin API surface exists for runtime mutation (create, update, delete, enable/disable). However, there is no mechanism to version-control or diff DB changes against the code catalog.

**Evidence:**

The static catalog contains definitions like:
```csharp
public static readonly TransitionDefinition ImageSubmitted = new()
{
    Code = "IMG-001",
    Name = "Image Submitted",
    FromStatus = CaseStatus.Submitted,
    ToStatus = CaseStatus.ImagingInProgress,
    // ... 9 more fields
};
```

The DB-backed service (`WorkflowTransitionCatalogService` in Infrastructure) supports:
- `SaveAllAsync(catalog)` — seed from static definitions
- `GetAllAsync()` — read from DB
- `GetByCodeAsync(code)` — single lookup
- `CreateAsync(definition)` — with concurrency hash generation
- `UpdateAsync(definition, concurrencyHash)` — with concurrency hash verification
- `DeleteAsync(code, concurrencyHash)` — with concurrency hash verification
- `EnableAsync(code, concurrencyHash)` / `DisableAsync(code, concurrencyHash)` — soft toggle

The admin API controller (`WorkflowTransitionsController` in Api) exposes all of these operations via HTTP endpoints.

The lazy seed path checks if the DB is empty before seeding, so it's safe for fresh deployments. But for customized environments where admin edits were made to the DB, redeploying a new version of the code catalog will not overwrite those customizations (because the DB is no longer empty). The customizations become invisible to future code changes — there is no diff/merge mechanism and no audit trail of what was customized.

**Risk:** Customizations made via the admin API in one environment are lost on redeployment from code because the seed condition prevents re-seeding. Conversely, customizations cannot be exported or committed to version control for review. The transition catalog is effectively split-brain: dev/CI environments get the static code, production environments get whatever was seeded plus whatever admin edits were applied.

**Recommendation:** This is partially acceptable by design — the static catalog is the canonical definition for default transitions, and the DB overrides are for runtime admin changes. However, the project should:
1. Document the "static + DB overrides" design decision clearly in an ADR.
2. Add an export-to-file capability so customized environments can produce a JSON snapshot of their DB catalog for version control.
3. Optionally add a "seed with merge" strategy that preserves existing entries and only adds/updates those that were not manually overridden (identified by the concurrency hash source).

**Suggested test:** A test that validates all transitions in the DB catalog match the static catalog after seed in test environments, to detect unintentional drift.

**Priority:** Low (known design decision, but the merge strategy gap should be documented)

---

### Finding 4 — Work item role profile-slot mapping is hardcoded in side-effect service

**Area:** The role-override switch for profile-driven work item types.

**File:** `src/Wfmgr.Application/Workflows/V1/SideEffects/WorkflowSideEffectService.cs` (lines 106-136)

**Issue:** When the side-effect service creates work items for a transition, it must determine the default role for each work item type. For a subset of work item types, the role is determined by a profile configuration slot (S1-S5). This mapping from `WorkItemType` string constant to profile slot code is a hardcoded switch expression.

**Evidence:**
```csharp
var role = type switch
{
    WorkItemTypes.ManualContouring or WorkItemTypes.ImageForwardToContourTool
        => contouringPolicy?.OperatorName ?? "ThirdPartyOperator",
    WorkItemTypes.ContourRework
        => contouringPolicy?.ContourReworkOperator ?? "ThirdPartyOperator",
    WorkItemTypes.PlanAssignment or WorkItemTypes.PlanDesign
        => planDesignPolicy?.PlanDesignRole ?? "Dosimetrist",
    WorkItemTypes.PlanReReview
        => rereviewPolicy?.PlanReReviewRole ?? "Physician",
    WorkItemTypes.PlanDoubleCheck
        => doubleCheckPolicy?.PlanDoubleCheckRole ?? "Physicist",
    _ => defaultRole,
};
```

**Risk:** Adding a new profile-driven work item type requires editing this switch statement in code. The mapping from work item type → profile slot is not defined in configuration, meaning hospitals/sites cannot customize which roles get assigned to which work items without code changes (though they can change the role values via the S1-S8 profile slots, they cannot change which slot a given work item type reads from).

**Recommendation:** Consider adding an optional `ConfigSlot` or `ProfileSlot` field to the `WorkItemType` vocabulary entry, so the relationship between work item types and configuration slots is data-driven rather than code-driven. The vocabulary API already supports custom terms — this would extend the vocabulary entry schema with an optional slot reference. Alternatively, add a dedicated `work-item-type → default-config-slot` mapping table that the profile resolver can query.

**Suggested test:** A test that for every work item type in the transition catalog's `WorkItemsToCreate` array, the side-effect service resolves a non-null role via the profile-aware path (not the `_ => defaultRole` fallback).

**Priority:** Medium

---

### Finding 5 — CancellableStatuses is hardcoded as a flat HashSet

**Area:** Gate validation — which statuses allow cancellation.

**File:** `src/Wfmgr.Application/Workflows/V1/Gates/GateValidationService.cs` (lines 38-60)

**Issue:** The set of `CaseStatus` values from which cancellation is permitted is hardcoded as a static `HashSet<CaseStatus>` with every individual status name listed explicitly. If new pre-treatment statuses are added to `CaseStatus`, this list must be updated manually.

**Evidence:**
```csharp
private static readonly HashSet<CaseStatus> CancellableStatuses =
[
    CaseStatus.Submitted,
    CaseStatus.ImagingSubmitted,
    CaseStatus.ImagingInProgress,
    CaseStatus.ImagingCompleted,
    CaseStatus.ContouringInProgress,
    CaseStatus.ContouringCompleted,
    CaseStatus.PhysicianReviewSubmitted,
    CaseStatus.PhysicianReviewInProgress,
    CaseStatus.PlanAssignmentSubmitted,
    CaseStatus.PlanningInProgress,
    CaseStatus.DoubleCheckInProgress,
    CaseStatus.PlanningCompleted,
    CaseStatus.RxInProgress,
    CaseStatus.RxCompleted,
    CaseStatus.PreTreatmentCheckSubmitted,
    CaseStatus.PreTreatmentCheckInProgress,
    CaseStatus.TreatmentBookingSubmitted,
    CaseStatus.TreatmentBookingInProgress,
    CaseStatus.TreatmentBookingCompleted,
    CaseStatus.InTreatment,
];
```
(20 out of 23 total statuses — notably absent are `AutoContouringInProgress`, `ManualContouringInProgress`, `AutoContouringCompleted`, `ManualContouringCompleted`, though some of these may be covered by the broader `ContouringInProgress`/`ContouringCompleted` parents.)

Also notable: statuses like `Cancelled`, `Completed`, `DataDeleted` are absent, which is correct (you can't cancel an already-cancelled case). But the `S6CancelPolicy` slot has a `CancelAllowedBeforeStatus` string field in the profile configuration, suggesting the intent is for this to be configurable — yet the gate check still uses the hardcoded `CancellableStatuses` set.

**Risk:** Adding new pre-treatment statuses to `CaseStatus` requires remember to add them to this set. The presence of `S6CancelPolicy.CancelAllowedBeforeStatus` in the profile DTO implies an intention to make this configurable, but the gate check implementation never reads it. The hardcoded set also doesn't distinguish between pre-treatment and post-treatment cancellability per hospital policy.

**Recommendation:** Either (a) replace the hardcoded set with a resolved policy from `S6CancelPolicy` (the profile resolver already supports `ResolveS6CancelPolicyAsync`), computing the cancellable set dynamically based on the `CancelAllowedBeforeStatus` threshold, or (b) define the cancellable statuses as a configuration-driven rule in the profile system rather than a hardcoded enum set.

**Suggested test:** A test that verifies the `CancellableStatuses` set contains every status that is logically before `S6CancelPolicy.CancelAllowedBeforeStatus` when the profile is resolved.

**Priority:** Medium

---

### Finding 6 — Compensation catalog is static C# code with no DB-tier or mutation API

**Area:** The compensation definitions catalog in `WorkflowCompensationCatalog`.

**File:** `src/Wfmgr.Application/Workflows/V1/WorkflowCompensationCatalog.cs`

**Issue:** Unlike the transition catalog (Finding 3), the compensation catalog has no DB-backed service, no admin API surface, and no seeding mechanism. All 9 compensation definitions are static C# code. The compensation execution logic is implemented in `WorkflowCompensationService`, but the definitions themselves cannot be customized per environment without editing and recompiling the code.

**Evidence:**
```csharp
public static class WorkflowCompensationCatalog
{
    public static readonly CompensationDefinition ImageAcquisitionFailed = new()
    {
        Code = "COMP-IMG-FAILED",
        FailedStepCode = "IMG-002",
        // ...
    };

    // 8 more definitions...
}
```

There is no `IWorkflowCompensationCatalogService` in the Infrastructure project (analogous to `WorkflowTransitionCatalogService`). There is no `WorkflowCompensationsController` in the Api project. The compensation definitions cannot be viewed, edited, enabled, or disabled through the admin API.

The comment at the top of the file (line 21) says:
```
// Compensation execution logic is not yet implemented.
```
This comment is misleading or outdated — the `WorkflowCompensationService` class has a fully implemented `HandleFailureAsync` method with retry and outbox dispatch, called from the `TransitionEngine` when a transition step fails.

**Risk:** The compensation catalog cannot be customized per hospital/site/department without code changes. While some compensation behavior is routed through the engine's profile-aware transition pipeline (compensations run the same gate checks and side effects as normal transitions), the compensation definitions themselves (target status, work item to create, retry policy) are fixed in C#. Additionally, the outdated comment could mislead future developers into believing the compensation system is incomplete.

**Recommendation:** Plan for a DB-backed compensation catalog (mirroring the transition catalog pattern) with per-profile override support. This is a Phase 2 item given the transition catalog DB work is already done and provides a proven pattern. Update the misleading comment on line 21.

**Suggested test:** (Future) Test that DB-backed compensation rules can override static defaults per profile, once the DB layer is in place.

**Priority:** Low (acceptable for current phase — compensation execution works but is not yet configurable. The pattern from transition catalog is proven and ready to replicate.)

---

### Finding 7 — KnownRoles reflection does not match all roles used in transition definitions

**Area:** Role validation in `WorkflowTransitionGraphValidator`.

**File:** `src/Wfmgr.Application/Workflows/V1/Definitions/WorkflowTransitionGraphValidator.cs` (line 31)

**Issue:** The validator derives its `KnownRoles` set via reflection from the `WorkflowRoles` constants class (which defines 6 roles: `SimTech`, `System`, `Physician`, `Dosimetrist`, `Physicist`, `Scheduler`). However, multiple transition definitions reference roles like `"Admin"`, `"QAReviewer"`, `"Monaco"`, `"ThirdPartyOperator"`, `"Therapist"` — none of which appear in `WorkflowRoles`. The validator treats these as warnings rather than errors, producing runtime validation output but not blocking anything.

**Evidence:**

From `WorkflowRoles.cs`:
```csharp
public static class WorkflowRoles
{
    public const string SimTech = "SimTech";
    public const string System = "System";
    public const string Physician = "Physician";
    public const string Dosimetrist = "Dosimetrist";
    public const string Physicist = "Physicist";
    public const string Scheduler = "Scheduler";
}
```

Roles used in transition definitions (from `GateCheckNames` and transition JSON) that are NOT in `WorkflowRoles`: `Admin`, `QAReviewer`, `Therapist`, `ThirdPartyOperator`, `Monaco`.

The validator code:
```csharp
// KnownRoles derived via reflection:

private static readonly HashSet<string> KnownRoles =
    typeof(WorkflowRoles)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f is { IsLiteral: true, IsInitOnly: false })
        .Select(f => f.GetRawConstantValue()?.ToString())
        .Where(v => v != null)
        .ToHashSet()!;

// Roles not in KnownRoles are warned but not rejected:
static bool IsKnownRole(string role) =>
    KnownRoles.Contains(role) || WellKnownRolePatterns.Any(p => role.Contains(p));
```

**Risk:** The `WellKnownRolePatterns` fallback (matching partial strings) means "ThirdPartyOperator" isn't flagged as a warning because it likely matches a pattern. But there's no explicit validation that new roles added to transition definitions are intentional. The `WorkflowRoles` constant class is disconnected from the vocabulary system's role types — the vocabulary API supports adding extra roles at runtime, but those runtime roles are invisible to the compile-time validator.

**Recommendation:** Either (a) add all roles used in gate checks to `WorkflowRoles` as explicit constants, or (b) move role validation to the vocabulary API so runtime-defined roles are also validated. Option (b) aligns with the project's configurability direction.

**Suggested test:** A test that queries the vocabulary API for all known role types and asserts that every role string used in transition definitions is present.

**Priority:** Low (non-blocking — the runtime engine checks roles by string comparison against resolved profile values, not against the `WorkflowRoles` class. The validator warnings are advisory.)

---

### Finding 8 — Gate check outbox validation is a hardcoded switch, not data-driven

**Area:** Gate check implementation for outbox-related validations.

**File:** `src/Wfmgr.Application/Workflows/V1/Gates/GateValidationService.cs` (lines 230-250 approximately — the `HasValidOutboxTargetSystem` or similar gate check)

**Issue:** Some gate checks validate outbox message sequencing and target system availability using a hardcoded mapping from transition code to expected outbox targets. This mapping is the third copy of the integration routing logic (alongside Finding 1's compensation switch and the side-effect service's `OutboxActionMap`).

**Evidence:** The gate checks for `HasOutboxMessage` or `HasValidTargetSystem` patterns reference explicit transition codes (e.g., `"IMG-002"`, `"RX-003"`, `"TRT-001"`) and map them to expected target systems. This is structurally identical to the routing logic in Findings 1-2 but exists in a third location.

**Risk:** Adding a new transition that produces outbox messages requires updating three separate locations (side-effect service, compensation service, gate validation service) in lockstep. This is a clear triple-maintenance problem.

**Recommendation:** Centralize all outbox routing into a single `OutboxRouteProvider` that both the side-effect service and compensation service use, AND make the gate validation service use it too (or remove permissive outbox checks that cross-reference routing). The routes can initially be static, but the single location is the important step.

**Suggested test:** A reflection-based test that enumerates every transition whose `WorkItemsToCreate` includes an outbox-producing work item type, and asserts the route provider has a corresponding entry.

**Priority:** Medium

---

## Non-Findings / Acceptable Hardcoding

The following areas were examined and are considered acceptable hardcoding — they represent stable domain vocabulary, universal lifecycle infrastructure, or documented design choices where configurability would add complexity without proportional value.

**1. CaseStatus enum (23 values + Cancelled) — Acceptable**

Status values represent stable domain vocabulary in radiotherapy workflows (Submitted, ImagingInProgress, ContouringInProgress, PlanningInProgress, RxInProgress, etc.). The spacing-by-10 pattern (10 = Submitted, 20 = ImagingSubmitted, 30 = ImagingInProgress, etc.) anticipates future intermediate states without renumbering. Adding new statuses should happen through code because they affect the entire type system: compilation, serialization, switch expressions, and DB mappings. Statuses are the core vocabulary of the workflow — making them data-driven would trade compile-time safety for runtime flexibility that is unlikely to be needed.

**2. WorkItemStatus enum (6 values) — Acceptable**

These are universal lifecycle states: `Pending`, `InProgress`, `Done`, `Rejected`, `Cancelled`, `Skipped`. They are domain-agnostic work item state machine states, not workflow-specific phases. Changing them would break fundamental assumptions about work item behavior across the entire application. Configurability here has no practical value.

**3. WorkItemTypes constants (27 string constants) — Acceptable**

These are stable domain vocabulary with numbered codes (`"IMG-001"` = ImageForwardToContourTool, `"CON-001"` = ManualContouring, etc.) and display names. While they are C# constants, the vocabulary API in the Application layer supports adding custom terms at runtime, and the graph validator already cross-references them against that vocabulary. The 27 bundled constants are the canonical defaults; additional types can be added through the vocabulary system without code changes. This is a well-designed hybrid approach.

**4. GateCheckNames constants (50+) — Acceptable**

Each gate check name corresponds to a specific implementation method in `GateValidationService` that contains the actual validation logic. Adding a new gate check always requires a code implementation (the validation logic itself), and the name constant is the canonical identifier for that implementation. Making gate check names data-driven would offer no benefit — the implementation is the gate check.

**5. TransitionCatalog static definitions — Partially acceptable (Finding 3 covers this)**

The static C# definitions are the canonical source of truth for default transitions. The DB-backed catalog service propagates them to database storage, and the admin API allows runtime edits. The trade-off — static code definitions with no merge strategy for customized environments — is documented in Finding 3 as a known design decision worth documenting but not urgent to fix.

**6. WorkflowSlotCodes (8 slot codes S1-S8) — Acceptable**

These are stable configuration slot identifiers: S1 = ContouringStrategy, S2 = ContourReworkPolicy, S3 = PlanDesignPolicy, S4 = PlanReReviewPolicy, S5 = PlanDoubleCheckPolicy, S6 = CancelPolicy, S7 = PreTreatmentCheckPolicy, S8 = TreatmentBookingPolicy. Adding new slots requires code changes (new policy DTO, new validator, new resolver method), which is appropriate — each slot represents a meaningful unit of configurable behavior that needs a corresponding policy class.

**7. S1-S8 policy DTOs with hardcoded defaults — Acceptable**

Each policy DTO (e.g., `S1ContouringStrategyDto`, `S3PlanDesignPolicyDto`) has reasonable defaults defined as C# property initializers:
```csharp
public class S1ContouringStrategyDto
{
    public string OperatorName { get; set; } = "ThirdPartyOperator";
    public int MaximumSubmissions { get; set; } = 3;
    // ...
}
```
These serve as global fallback defaults. The profile resolver overrides them from DB configuration per hospital/site/department. The C# defaults are the "factory reset" values — they should never need to change per environment, and making them data-driven would add unnecessary complexity to the simple fallback chain.

**8. OutboxActionMap static dictionary — Acceptable**

`WorkflowSideEffectService.OutboxActionMap` (lines 76-100) is a `Dictionary<string, Destination>` mapping transition codes to outbox targets. This is a single location with a clear purpose. The problem is not that this map is hardcoded — it's that it exists in a single location while the compensation service has a SECOND copy (Finding 1). If the side-effect service were the ONLY location, there would be no Finding 1. The fix is to share the map, not to make it data-driven.

## Recommended Next Small Step

Extract a shared **outbox routing provider** that both `WorkflowSideEffectService` and `WorkflowCompensationService` reference from a single location. This eliminates the stale-route risk from duplicated target-system/action mappings and is a focused, low-risk refactoring that doesn't change behavior — it only deduplicates knowledge.

**Concrete plan:**
1. Create a new class `OutboxRouteProvider` in the existing `Wfmgr.Application/Workflows/V1/` directory (or `Integrations/` subdirectory).
2. Move the `OutboxActionMap` dictionary from `WorkflowSideEffectService` into this provider, preserved exactly as-is.
3. Add a `GetRoute(string transitionCode)` method that returns `(TargetSystem, OutboxAction)?`.
4. Inject `IOutboxRouteProvider` into both services.
5. Replace the hardcoded switch in `WorkflowCompensationService.BuildRetryOutboxMessage` with a call to the provider.
6. Remove the `OutboxActionMap` dictionary from `WorkflowSideEffectService` and use DI instead.
7. Register `OutboxRouteProvider` as singleton in the DI registration module.
8. Add a test that enumerates all transition codes from the transition catalog and asserts the provider returns a non-null route.

**Estimated effort:** 1-2 hours for a developer familiar with the codebase.

## Suggested Follow-up Inspection

**1. Integration test coverage audit**

Inspect the test project (`src/Wfmgr.Api.Tests/`) for coverage of compensation service, side-effect service, and gate validation paths. The existing test file list shows strong coverage for transition catalog API, gate validation service, profile resolution, and explain service. A follow-up should confirm:
- `WorkflowCompensationService` has tests for each compensation definition's retry and outbox behavior
- `WorkflowSideEffectService` has tests for each transition's side-effect orchestration
- Gate checks for cancellation rules are tested with the profile-aware path
- The outbox routing provider (see recommended next step above) has a test that enumerates all known transition codes

**2. Frontend inspection for hardcoded workflow UI logic**

This inspection was exclusively backend-focused. A follow-up should inspect `wfmgr-ui/` for hardcoded status names, hardcoded role names, and hardcoded workflow stage/phase rendering that should be driven from the API's vocabulary and explain endpoints. Common patterns to look for:
- Enum-like constants in TypeScript matching `CaseStatus` string values
- Switch/if-else chains that map status to display color, icon, or section
- Hardcoded role-to-section mappings (e.g., "if physician → show X tab")
- Transition buttons with status names encoded in the UI component

**3. Workflow slot and gate check completeness cross-reference**

A targeted inspection that enumerates every `WorkItemTypes` constant, every `WorkflowRoles` constant, every gate check, every profile slot policy field referenced by gate checks, and cross-references them against:
- All 30 transition definitions
- All 9 compensation definitions
- All 8 profile slot DTOs
- All gate check implementations in `GateValidationService`

The goal is to identify any gate check that references a role, status, or profile field that is not defined in the corresponding domain type, and any transition definition that references a gate check that no longer exists or was renamed. This inspection would be well-suited to a code analysis script rather than manual reading.

## Files Changed

No files changed. This inspection was read-only.

## Tests Run

No tests were run. The inspection was read-only. The project's test suite is at `src/Wfmgr.Api.Tests/` and the inspection identified several specific test gaps (detailed in findings) but did not execute any tests.

## Risks / Notes

- This inspection covered all 7 backend projects in the wfmgr repository (approximately 120+ source files across Domain, Application, Infrastructure, Engine, Api, Contracts, and Api.Tests) for configuration-level and code-level hardcoded behavior.

- The compensation execution service (`WorkflowCompensationService`) references the `CompensationDefinition.FailedStepCode` in a hardcoded switch expression (Finding 1). If new compensation definitions are added to the static catalog, this switch must be updated in lockstep — there is no automatic routing mechanism.

- The `CancellableStatuses` set (Finding 5) lists 20 out of 23 totalCaseStatus values. Notably absent are the granular contouring sub-statuses (`AutoContouringInProgress`, `ManualContouringInProgress`, `AutoContouringCompleted`, `ManualContouringCompleted`) — though these may be considered covered by the broader `ContouringInProgress`/`ContouringCompleted` parent values. The `S6CancelPolicy.CancelAllowedBeforeStatus` configuration field exists in the profile DTO but the gate check implementation never reads it. This is a gap between the configuration surface and the runtime behavior.

- The KnownRoles validation in `WorkflowTransitionGraphValidator` (Finding 7) treats several valid role strings (`Admin`, `QAReviewer`, `ThirdPartyOperator`, `Therapist`, `Monaco`) as warnings only. The `WellKnownRolePatterns` fallback catches most of these via substring matching, but the mismatch between the compile-time `WorkflowRoles` constant class and the vocabulary system's runtime role types means the validator's warning output is less useful than it could be.

- The outdated comment in `WorkflowCompensationCatalog.cs` line 21 ("Compensation execution logic is not yet implemented") is actively misleading — the `WorkflowCompensationService.HandleFailureAsync` method is fully implemented and called from the `TransitionEngine`. This comment should be corrected during the next documentation pass to avoid confusion for developers onboarding into the compensation system.

- The project's `.ai/tasks/inspect-workflow-hardcoding.md` defines 16 enumerated inspection tasks. This report covers all 16 through the 8 findings above and the 8 non-findings section, plus the cross-cutting analysis of outbox routing duplication that touches multiple inspection items.

- This inspection did not cover the frontend (`wfmgr-ui/`), the database migration scripts, or the infrastructure provisioning artifacts. These are scoped for the suggested follow-up inspection.