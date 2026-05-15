# Prompt F — Replace hardcoded workflow values with DB-backed configurable options

> Self-contained implementation prompt for the wfmgr radiotherapy workflow manager
> repository. Builds on Prompts A–E (single state machine, typed `RequiredRoles`,
> concurrency hardening + changelog, slot-code validation, explainability endpoint).
> All 24 existing tests must still pass; new behaviour gated by new tests.

## Goal

Push the remaining clinically-relevant hardcoded policy values out of code and
into the database so they can be edited per `(hospital, site, department)` profile
through the existing admin UI. The frontend `workflow-config.page` must surface
the new slot and the new "where did this value come from?" indicator.

## Scope (explicitly bounded)

In scope:
1. New slot **`S0_CASE_INTAKE_POLICY`** controlling the device / device-type /
   work-item type / role / auto-start-trigger that today are hardcoded inside
   [CaseWorkflowService.CreateCaseAsync](Wfmgr.Application/Workflows/V1/CaseWorkflowService.cs)
   (lines ~100–116, the `XVI` / `CT` / `WorkflowRoles.SimTech` /
   `WorkItemTypes.DailyImageScan` block).
2. New slot **`S9_EXTERNAL_EVENT_MAPPING`** that maps external integration source
   codes (today the magic strings `"CT"`, `"PVMED"`,
   `"PVMED_AUTOCONTOUR_COMPLETED"`, `"PVMED_AUTOCONTOUR_FAILED"`) to canonical
   trigger names. Replaces the hardcoded comparisons in
   [CaseWorkflowService.HandleCtImageStoredAsync](Wfmgr.Application/Workflows/V1/CaseWorkflowService.cs)
   (line ~167) and `HandlePvMedEventAsync` (line ~271).
3. **Seeded "Default" profile** in DB so the resolver never invents defaults.
   Defaults that today live in
   [S1ContouringStrategy.cs](Wfmgr.Application/Workflows/V1/S1ContouringStrategy.cs)
   and the other slot record initializers move into the seeded rows. Resolver
   becomes strict: if no rule matches even the seed profile, log a warning *and*
   return the type's default (so behaviour is preserved) but mark the result so
   callers / UI know it was unconfigured.
4. **Frontend** updates to
   [wfmgr-ui/src/app/pages/workflow-config/workflow-config.page.ts](wfmgr-ui/src/app/pages/workflow-config/workflow-config.page.ts)
   and `.html`:
   - Add S0 and S9 to the slot dropdown automatically (driven by
     `GET /api/workflow-config/slot-codes`).
   - Render a per-slot **provenance badge** on the "Effective preview" panel:
     `Customer rule` / `Default profile` / `Code default (warning)` based on a
     new field returned by `GET /api/workflow-config/effective`.
   - Provide a "Reset to default" button on the rule editor that pre-fills
     `configJson` with the seeded default profile's value for the same slot.

Out of scope (explicit):
- Rewriting `WorkflowTransitionCatalog` to be DB-driven (kept code-owned).
- Touching the `CaseStatus`, `WorkItemTypes`, `CaseFormTypes`, or `WorkflowRoles`
  enums/static classes.
- The `ForwardToMonacoAsync` short-circuit and its synthetic
  `ContourReviewForm` evidence (deferred to a separate prompt).
- Compensation outbox routing (#14 in review).

## Existing context the implementer needs

- Solution: [wfmgr.sln](wfmgr.sln). Build: `dotnet build wfmgr.sln -c Debug`.
  Test: `dotnet test wfmgr.sln --no-build -c Debug --nologo` (24 tests).
- .NET 10, EF Core 8 (Npgsql); InMemory provider used by xUnit tests.
- `Microsoft.AspNetCore.Mvc.Testing 10.0.5` is required on net10 (8.x breaks).
- EF tooling: `dotnet tool restore` then
  `dotnet ef migrations add <name> --project Wfmgr.Infrastructure/Wfmgr.Infrastructure.csproj --startup-project Wfmgr.Api/Wfmgr.Api.csproj --output-dir Persistence/Migrations`.
- Slot constants live in
  [WorkflowSlotCodes.cs](Wfmgr.Application/Workflows/V1/WorkflowSlotCodes.cs).
- Slot DTOs returned by `GET /api/workflow-config/slot-codes` come from
  [WorkflowConfigService.GetSlotCodes](Wfmgr.Infrastructure/Profiles/WorkflowConfigService.cs)
  (~ line 295).
- Resolver fallback is in
  [WorkflowProfileResolver.cs](Wfmgr.Infrastructure/Profiles/WorkflowProfileResolver.cs)
  (~ line 126); today it silently falls back to `fallbackFactory()` when the
  profile or rule is missing (#20 in the architecture review).
- Slot config validator: [WorkflowSlotConfigValidator.cs](Wfmgr.Application/Workflows/V1/WorkflowSlotConfigValidator.cs).
- Angular standalone app already wires `WorkflowApiService`; the page reads
  `slotCodes`, `profiles`, `rules`, and the effective preview through that
  service. The page and its template are listed above.

## Backend tasks

### B1. Define the two new slot policies

Create two new files next to the existing slot record types:

**`Wfmgr.Application/Workflows/V1/S0CaseIntakePolicy.cs`**
```csharp
namespace Wfmgr.Application.Workflows.V1;

public class S0CaseIntakePolicy
{
    public bool AutoCreateIntakeWorkItem { get; set; } = true;
    public string IntakeWorkItemType { get; set; } = "DailyImageScan";
    public string AssignedRole { get; set; } = "SimTech";
    public string Device { get; set; } = "XVI";
    public string DeviceType { get; set; } = "CT";
    public bool AutoStartSimulation { get; set; } = true;
}
```

**`Wfmgr.Application/Workflows/V1/S9ExternalEventMappingPolicy.cs`**
```csharp
namespace Wfmgr.Application.Workflows.V1;

public class S9ExternalEventMappingPolicy
{
    public IList<ExternalEventMappingEntry> Mappings { get; set; } = new List<ExternalEventMappingEntry>();
}

public class ExternalEventMappingEntry
{
    public string SourceSystem { get; set; } = ""; // e.g. "CT", "PVMED", "MyHospitalRis"
    public string SourceEventType { get; set; } = ""; // e.g. "IMAGE_STORED", "PVMED_AUTOCONTOUR_COMPLETED"
    public string CanonicalTriggerName { get; set; } = ""; // e.g. "StoreImage", "AutoContourCompleted"
}
```

Add the slot codes:

**`Wfmgr.Application/Workflows/V1/WorkflowSlotCodes.cs`** — append:
```csharp
public const string S0CaseIntakePolicy = "S0_CASE_INTAKE_POLICY";
public const string S9ExternalEventMapping = "S9_EXTERNAL_EVENT_MAPPING";
```

Wire validator + slot-list:
- Extend `WorkflowSlotConfigValidator.Validate` to validate both new types.
  S0 must reject empty `IntakeWorkItemType` / `AssignedRole`. S9 must reject
  duplicate `(SourceSystem, SourceEventType)` pairs and require all three string
  fields per entry.
- Extend `WorkflowConfigService.GetSlotCodes()` to include S0 and S9 with
  human-readable name/description.
- Extend `WorkflowConfigService.TryParseSlotConfig` switch.
- Extend `WorkflowProfileResolver` with `ResolveS0CaseIntakePolicyAsync` and
  `ResolveS9ExternalEventMappingAsync` paralleling existing `ResolveS1...Async`.

### B2. Strict resolver with provenance

Refactor `WorkflowProfileResolver`:

```csharp
public enum SlotResolutionSource { CustomerRule, DefaultProfileRule, CodeDefault }

public sealed record SlotResolution<TConfig>(
    TConfig Config,
    SlotResolutionSource Source,
    Guid? RuleId,
    Guid? ProfileId,
    string? Warning);
```

Add `ResolveSlotWithProvenanceAsync<TConfig>(...)` that:
1. Tries the customer-scope profile (existing logic).
2. If no rule matches, falls back to a profile keyed by
   `HospitalId == null && SiteId == null && DepartmentId == null`, version `int.MaxValue`,
   `Name == "Default"`. If a matching enabled rule exists there, return
   `SlotResolutionSource.DefaultProfileRule`.
3. Otherwise return `SlotResolutionSource.CodeDefault` with `fallbackFactory()`
   and log a `_logger.LogWarning("Slot {Slot} resolved from code default — no rule in any profile", slotCode);`.

Existing `ResolveS1ContouringStrategyAsync` etc. delegate to this and unwrap
`Config` for backward compatibility. Add a public method
`ResolveAllProvenanceAsync(string? hospitalId, string? siteId, string? departmentId, CancellationToken ct)`
that returns `IReadOnlyDictionary<string, SlotResolutionSource>` keyed by slot
code — used by the effective endpoint.

### B3. Default profile seeder

Create
**`Wfmgr.Infrastructure/Profiles/WorkflowDefaultProfileSeeder.cs`** implementing
`IHostedService` (or invoked from `Wfmgr.Infrastructure.DependencyInjection`'s
startup hook). On startup it:

1. Ensures a profile exists where
   `HospitalId == null && SiteId == null && DepartmentId == null && Name == "Default"`
   (idempotent — match by `Name == "Default"` AND all three scope fields null).
2. Inserts one rule per slot code S0–S9 with `Priority = 0`, `IsEnabled = true`,
   `ConfigJson = JsonSerializer.Serialize(new SXPolicy())` (i.e. the type's
   built-in C# defaults).
3. Skips inserting any rule that already exists for the seed profile + slot.

Register the hosted service from
[Wfmgr.Api/Program.cs](Wfmgr.Api/Program.cs). Skip seeding when running under
the InMemory provider or when an env var `WFMGR_SKIP_SEED=1` is set (so tests
can opt out by default and assert seeding behaviour explicitly when needed).

### B4. Wire the new slots into runtime

Modify [CaseWorkflowService.CreateCaseAsync](Wfmgr.Application/Workflows/V1/CaseWorkflowService.cs)
(~ line 100):

```csharp
var intake = await _profileResolver.ResolveS0CaseIntakePolicyAsync(
    request.HospitalId, request.SiteId, request.DepartmentId, ct);

if (intake.AutoCreateIntakeWorkItem)
{
    await _dataAccess.AddWorkItemAsync(new WorkItemData
    {
        CaseId = caseId,
        Type = intake.IntakeWorkItemType,
        AssignedRole = intake.AssignedRole,
        ExternalCorrelationId = intake.Device,
        PayloadJson = JsonSerializer.Serialize(new
        {
            device = intake.Device,
            deviceType = intake.DeviceType,
            request.AccessionNumber,
            notes = request.Notes
        }),
        CreatedAtUtc = now
    }, ct);
}

if (intake.AutoStartSimulation)
{
    // existing AutoStartSimulation transition
}
```

Modify `HandleCtImageStoredAsync` and `HandlePvMedEventAsync` to consult
`ResolveS9ExternalEventMappingAsync` for the canonical trigger name. The
existing string equality checks become:

```csharp
var mapping = await _profileResolver.ResolveS9ExternalEventMappingAsync(
    caseData.HospitalId, caseData.SiteId, caseData.DepartmentId, ct);

string CanonicalTrigger(string source, string type) =>
    mapping.Mappings.FirstOrDefault(m =>
            string.Equals(m.SourceSystem, source, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(m.SourceEventType, type, StringComparison.OrdinalIgnoreCase))
        ?.CanonicalTriggerName ?? type; // fallback: pass through
```

The seeded `Default` profile must contain S9 mappings that reproduce the
current hardcoded behaviour exactly so existing tests keep passing:

| SourceSystem | SourceEventType | CanonicalTriggerName |
|---|---|---|
| `CT` | `IMAGE_STORED` | `StoreImage` |
| `PVMED` | `PVMED_AUTOCONTOUR_COMPLETED` | `AutoContourCompleted` |
| `PVMED` | `PVMED_AUTOCONTOUR_FAILED` | `AutoContourFailed` |

Idempotency lookups (`ExternalEventExistsAsync("CT", "IMAGE_STORED", ...)`)
keep using the raw source/type pair — only the trigger-name dispatch is now
data-driven.

### B5. Effective endpoint exposes provenance

Extend [`EffectiveWorkflowSlotDto`](Wfmgr.Application/Workflows/V1/Config/WorkflowConfigDtos.cs)
with `string Source` (one of `"CustomerRule" | "DefaultProfileRule" | "CodeDefault"`)
and `string? Warning`. Populate it in
`WorkflowConfigService.GetEffectiveConfigAsync` using the new
`ResolveAllProvenanceAsync` so the UI can render the badge.

## Frontend tasks

In [wfmgr-ui/src/app/pages/workflow-config/workflow-config.page.ts](wfmgr-ui/src/app/pages/workflow-config/workflow-config.page.ts)
and `.html`:

1. Slot dropdown is already driven by `slotCodes`; nothing to add there. Verify
   that the page renders S0 and S9 once the backend returns them.
2. Update the `EffectiveWorkflowSlot` model
   ([wfmgr-ui/src/app/core/models/workflow.models.ts](wfmgr-ui/src/app/core/models/workflow.models.ts))
   with the new `source` and `warning` fields.
3. In the effective-preview list, render a chip next to each slot:
   - `Customer` (green) for `CustomerRule`
   - `Default` (blue) for `DefaultProfileRule`
   - `Code default ⚠` (amber) for `CodeDefault`
   When `warning` is non-null, render it as a tooltip.
4. On the rule editor, add a `Reset to default` button that calls
   `GET /api/workflow-config/profiles/{defaultProfileId}/...` (re-use the
   effective endpoint with empty scope to discover the default profile id) and
   patches `ruleForm.configJson` with the JSON returned for the currently
   selected `slotCode`. If the slot is `S9_EXTERNAL_EVENT_MAPPING`, render the
   value pretty-printed.

Do not rewrite the JSON editor; keep the textarea but format on reset.

## Tests (must add)

Add to `Wfmgr.Api.Tests/`:
- `WorkflowDefaultSeederTests.cs`: spins up a factory with seeding **enabled**
  (override `WFMGR_SKIP_SEED=0`); asserts that GET
  `/api/workflow-config/profiles` returns a profile named `"Default"` with
  rules for every slot in `WorkflowSlotCodes`.
- Extend `WorkflowExplainApiTests` (or a new file) with a test that hits
  `GET /api/workflow-config/effective` for an unknown scope and asserts each
  resolved slot has `source == "CodeDefault"` *or* `"DefaultProfileRule"`
  (depending on whether seeding ran). No slot may have `source == null`.
- Add `CaseIntakePolicyOverrideTests.cs`: seeds a customer profile with an S0
  rule (`AutoCreateIntakeWorkItem=false`); asserts that
  `POST /api/cases` for that scope produces a case with **no** `DailyImageScan`
  work item.
- Add `ExternalEventMappingOverrideTests.cs`: seeds an S9 rule that maps
  `("MyRis", "STORED")` → `StoreImage`, posts an external event with that
  source/type, asserts the case still advances to `ImageStored`.
- All four existing test classes' factories must still suppress
  `ManyServiceProvidersCreatedWarning` (already done in the prior task — keep it).

## Acceptance criteria

- `dotnet build wfmgr.sln -c Debug` reports 0 warnings, 0 errors.
- `dotnet test wfmgr.sln --no-build -c Debug --nologo` is green; total test
  count ≥ 27 (24 existing + at least 3 new). All previously-passing tests must
  still pass without modification of their assertions.
- A fresh `dotnet ef migrations add` produces *only* changes related to seeded
  data / no schema changes are required for this prompt (S0/S9 reuse the
  existing `WorkflowRule` table). Do not create a migration unless EF demands one.
- Running the API against an empty PostgreSQL DB (after `dotnet ef database
  update`) and starting it once produces exactly one `Default` profile with
  ten rules (S0–S9) on subsequent restarts.
- Hitting `GET /api/workflow-config/effective?hospitalId=NEW` against the
  seeded DB returns every slot with `source == "DefaultProfileRule"` and no
  warnings.
- Loading the Angular admin UI shows the new slots in the dropdown and the
  provenance chip on the effective preview.

## Implementation hints / pitfalls observed in prior prompts

- The four xUnit factories instantiate distinct `IDbContextOptions`. Keep the
  `ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))`
  call when copying any factory to a new test class.
- For `multi_replace_string_in_file` against `WorkflowTransitionCatalog`-style
  files with many similar entries, anchor each replacement on the unique
  business code field (`Code = "..."`).
- `dotnet ef migrations add` against this repo emits many EF informational
  warnings — they are noise, not errors.
- The seeder must not use `services.AddHostedService` if you want it to skip
  during tests; either gate it on `IHostEnvironment.IsEnvironment("Testing")`
  or inspect the `DbContextOptions` to detect the InMemory provider.
- `WorkflowProfileResolver` uses `JsonSerializer` with
  `PropertyNameCaseInsensitive = true`; the seeder must serialize using the
  same options to round-trip.

## Deliverables

1. New files:
   - `Wfmgr.Application/Workflows/V1/S0CaseIntakePolicy.cs`
   - `Wfmgr.Application/Workflows/V1/S9ExternalEventMappingPolicy.cs`
   - `Wfmgr.Infrastructure/Profiles/WorkflowDefaultProfileSeeder.cs`
   - Test files listed above.
2. Modified files:
   - `Wfmgr.Application/Workflows/V1/WorkflowSlotCodes.cs`
   - `Wfmgr.Application/Workflows/V1/WorkflowSlotConfigValidator.cs`
   - `Wfmgr.Application/Workflows/V1/CaseWorkflowService.cs`
   - `Wfmgr.Application/Workflows/V1/Config/WorkflowConfigDtos.cs`
   - `Wfmgr.Infrastructure/Profiles/WorkflowProfileResolver.cs`
   - `Wfmgr.Infrastructure/Profiles/WorkflowConfigService.cs`
   - `Wfmgr.Infrastructure/DependencyInjection.cs` *or* `Wfmgr.Api/Program.cs`
     for hosted-service registration.
   - `wfmgr-ui/src/app/pages/workflow-config/workflow-config.page.ts` and `.html`
   - `wfmgr-ui/src/app/core/models/workflow.models.ts`
3. No documentation files.
4. End the implementation with the build + test commands above and report counts.
