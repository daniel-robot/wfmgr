using Microsoft.EntityFrameworkCore;
using Npgsql;
using Wfmgr.Infrastructure.Persistence;
using Wfmgr.Infrastructure.Persistence.Entities;

namespace Wfmgr.Infrastructure.Profiles;

/// <summary>
/// Lazily seeds the two default <see cref="WorkflowProfileEntity"/> rows
/// (global + RT department) together with their 16 default
/// <see cref="WorkflowRuleEntity"/> slot rules (8 per profile) when the
/// <c>WorkflowProfile</c> table is empty.
/// <para>
/// Mirrors the seed in <c>database/init.sql</c> verbatim — same profile and
/// rule GUIDs, same JSON payloads — so creating an empty database and running
/// EF migrations (without the docker-compose init script) produces an
/// identical default configuration. The two rule sets differ intentionally:
/// the department profile uses "engaged" defaults (auto-contour ON, escalation
/// ON, re-review ON, double-check ON), the global profile uses safer
/// "opt-out" defaults.
/// </para>
/// <para>
/// Follows the same race-safe pattern used by
/// <c>WorkflowTransitionCatalogService.EnsureSeededAsync</c>.
/// </para>
/// </summary>
internal static class WorkflowProfileSeeder
{
    // Process-wide one-shot guard: once we've successfully verified or seeded
    // the table in this process, subsequent resolver calls skip the AnyAsync.
    private static int _seeded;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static readonly Guid GlobalProfileId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DepartmentProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static async Task EnsureSeededAsync(WfmgrDbContext db, CancellationToken ct)
    {
        if (Volatile.Read(ref _seeded) == 1) return;

        await Gate.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref _seeded) == 1) return;

            if (await db.WorkflowProfiles.AnyAsync(ct))
            {
                Volatile.Write(ref _seeded, 1);
                return;
            }

            var now = DateTimeOffset.UtcNow;

            db.WorkflowProfiles.Add(new WorkflowProfileEntity
            {
                ProfileId = GlobalProfileId,
                HospitalId = null,
                SiteId = null,
                DepartmentId = null,
                Name = "Global Default Workflow",
                Version = 1,
                IsActive = true,
                CreatedAt = now,
            });

            db.WorkflowProfiles.Add(new WorkflowProfileEntity
            {
                ProfileId = DepartmentProfileId,
                HospitalId = "HOSP001",
                SiteId = "SITE_A",
                DepartmentId = "RT",
                Name = "RT Department Workflow",
                Version = 1,
                IsActive = true,
                CreatedAt = now,
            });

            AddRules(db, DepartmentProfileId, DepartmentSlotConfigs, now);
            AddRules(db, GlobalProfileId, GlobalSlotConfigs, now);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsSeedRace(ex))
            {
                // A concurrent caller seeded first; safe to ignore once data exists.
                db.ChangeTracker.Clear();
            }

            Volatile.Write(ref _seeded, 1);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static bool IsSeedRace(DbUpdateException ex)
        => ex.InnerException is PostgresException pg
           && pg.SqlState == PostgresErrorCodes.UniqueViolation;

    private static void AddRules(
        WfmgrDbContext db,
        Guid profileId,
        (Guid RuleId, string SlotCode, string ConfigJson)[] configs,
        DateTimeOffset now)
    {
        foreach (var (ruleId, slotCode, configJson) in configs)
        {
            db.WorkflowRules.Add(new WorkflowRuleEntity
            {
                RuleId = ruleId,
                ProfileId = profileId,
                SlotCode = slotCode,
                Priority = 1,
                ConditionJson = null,
                ConfigJson = configJson,
                IsEnabled = true,
                EffectiveFrom = now,
                EffectiveTo = null,
                CreatedAt = now,
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Default slot configurations — mirror database/init.sql verbatim.
    // Rule GUIDs are deterministic so DB dumps from the docker-init'd and
    // migration-only paths diff cleanly:
    //   Department profile: 30000000-...-000000000001..008
    //   Global profile:     30000000-...-000000000101..108
    // ─────────────────────────────────────────────────────────────────────

    private static readonly (Guid RuleId, string SlotCode, string ConfigJson)[] DepartmentSlotConfigs =
    {
        (Guid.Parse("30000000-0000-0000-0000-000000000001"), "S1_CONTOURING_STRATEGY", """
            {
                "autoContourEnabled": true,
                "provider": "PvMed",
                "onAutoContourComplete": {
                    "autoForwardToMonaco": true,
                    "allowManualForward": true
                },
                "fallback": {
                    "onFailureCreateManualWorkItem": true,
                    "manualWorkItemType": "ManualContouring",
                    "manualWorkItemRole": "Physician"
                }
            }
            """),
        (Guid.Parse("30000000-0000-0000-0000-000000000002"), "S2_CONTOUR_REVIEW_POLICY", """
            {
                "reviewMode": "Single",
                "allowSecondReview": false,
                "onReject": {
                    "targetStatus": "ContourReworkRequired",
                    "createReworkWorkItem": true,
                    "reworkWorkItemRole": "Physician"
                },
                "timeoutHours": 24
            }
            """),
        (Guid.Parse("30000000-0000-0000-0000-000000000003"), "S3_PLAN_DISPATCH", """
            {
                "dispatchMode": "AutoAssignByRole",
                "targetRole": "Dosimetrist",
                "allowManualClaim": true,
                "slaMinutes": 240,
                "escalation": {
                    "enabled": true,
                    "afterMinutes": 180,
                    "escalateToRole": "Physician"
                }
            }
            """),
        (Guid.Parse("30000000-0000-0000-0000-000000000004"), "S4_PLAN_REREVIEW_POLICY", """
            {
                "enabled": true,
                "trigger": {
                    "riskLevelIn": ["High"],
                    "doseDeltaPercentGte": 5
                },
                "reviewRole": "Physicist",
                "onRejectBackTo": "PlanningInProgress"
            }
            """),
        (Guid.Parse("30000000-0000-0000-0000-000000000005"), "S5_PLAN_DOUBLE_CHECK", """
            {
                "enabled": true,
                "workItemRole": "QAReviewer",
                "requiresDifferentUserFrom": "PlanQA",
                "onFailBackTo": "PlanQAInProgress",
                "maxRetry": 1
            }
            """),
        (Guid.Parse("30000000-0000-0000-0000-000000000006"), "S6_CANCEL_POLICY", """
            {
                "allowCancel": true,
                "cancelAllowedBeforeStatus": "Treating",
                "requireCancelReason": true,
                "onCancel": {
                    "closeOpenWorkItems": true,
                    "createAudit": true,
                    "finalStatus": "Cancelled"
                }
            }
            """),
        (Guid.Parse("30000000-0000-0000-0000-000000000007"), "S7_TREATMENT_COMPLETION_POLICY", """
            {
                "mode": "ByCourseCompletedEvent",
                "requiredFractions": 30,
                "acceptCourseCompletedEvent": true,
                "allowManualCompletion": false,
                "onMismatch": {
                    "createExceptionWorkItem": true,
                    "exceptionRole": "Therapist"
                }
            }
            """),
        (Guid.Parse("30000000-0000-0000-0000-000000000008"), "S8_EXCEPTION_HANDLING_POLICY", """
            {
                "retry": {
                    "enabled": true,
                    "maxAttempts": 5,
                    "backoff": "Exponential",
                    "baseSeconds": 30
                },
                "manualFallback": {
                    "enabled": true,
                    "workItemType": "TreatmentExceptionHandling",
                    "workItemRole": "Admin"
                },
                "notify": {
                    "enabled": true,
                    "channels": ["InApp", "Email"]
                }
            }
            """),
    };

    private static readonly (Guid RuleId, string SlotCode, string ConfigJson)[] GlobalSlotConfigs =
    {
        (Guid.Parse("30000000-0000-0000-0000-000000000101"), "S1_CONTOURING_STRATEGY", """
            {
                "autoContourEnabled": false,
                "provider": "PvMed",
                "onAutoContourComplete": {
                    "autoForwardToMonaco": false,
                    "allowManualForward": true
                },
                "fallback": {
                    "onFailureCreateManualWorkItem": true,
                    "manualWorkItemType": "ManualContouring",
                    "manualWorkItemRole": "Physician"
                }
            }
            """),
        (Guid.Parse("30000000-0000-0000-0000-000000000102"), "S2_CONTOUR_REVIEW_POLICY", """
            {
                "reviewMode": "Single",
                "allowSecondReview": false,
                "onReject": {
                    "targetStatus": "ContourReworkRequired",
                    "createReworkWorkItem": true,
                    "reworkWorkItemRole": "Physician"
                },
                "timeoutHours": 24
            }
            """),
        (Guid.Parse("30000000-0000-0000-0000-000000000103"), "S3_PLAN_DISPATCH", """
            {
                "dispatchMode": "AutoAssignByRole",
                "targetRole": "Dosimetrist",
                "allowManualClaim": true,
                "slaMinutes": 240,
                "escalation": {
                    "enabled": false,
                    "afterMinutes": 180,
                    "escalateToRole": "Physician"
                }
            }
            """),
        (Guid.Parse("30000000-0000-0000-0000-000000000104"), "S4_PLAN_REREVIEW_POLICY", """
            {
                "enabled": false,
                "trigger": {
                    "riskLevelIn": [],
                    "doseDeltaPercentGte": null
                },
                "reviewRole": "Physicist",
                "onRejectBackTo": "PlanningInProgress"
            }
            """),
        (Guid.Parse("30000000-0000-0000-0000-000000000105"), "S5_PLAN_DOUBLE_CHECK", """
            {
                "enabled": false,
                "workItemRole": "QAReviewer",
                "requiresDifferentUserFrom": "PlanQA",
                "onFailBackTo": "PlanQAInProgress",
                "maxRetry": 1
            }
            """),
        (Guid.Parse("30000000-0000-0000-0000-000000000106"), "S6_CANCEL_POLICY", """
            {
                "allowCancel": true,
                "cancelAllowedBeforeStatus": "Treating",
                "requireCancelReason": true,
                "onCancel": {
                    "closeOpenWorkItems": true,
                    "createAudit": true,
                    "finalStatus": "Cancelled"
                }
            }
            """),
        (Guid.Parse("30000000-0000-0000-0000-000000000107"), "S7_TREATMENT_COMPLETION_POLICY", """
            {
                "mode": "ByCourseCompletedEvent",
                "requiredFractions": 30,
                "acceptCourseCompletedEvent": true,
                "allowManualCompletion": false,
                "onMismatch": {
                    "createExceptionWorkItem": true,
                    "exceptionRole": "Therapist"
                }
            }
            """),
        (Guid.Parse("30000000-0000-0000-0000-000000000108"), "S8_EXCEPTION_HANDLING_POLICY", """
            {
                "retry": {
                    "enabled": true,
                    "maxAttempts": 5,
                    "backoff": "Exponential",
                    "baseSeconds": 30
                },
                "manualFallback": {
                    "enabled": true,
                    "workItemType": "TreatmentExceptionHandling",
                    "workItemRole": "Admin"
                },
                "notify": {
                    "enabled": false,
                    "channels": ["InApp"]
                }
            }
            """),
    };

    // Test hook — allows tests using ephemeral per-test DBs to re-seed.
    internal static void ResetForTesting() => Volatile.Write(ref _seeded, 0);
}
