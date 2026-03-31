BEGIN;

DROP TABLE IF EXISTS "PlanVersion" CASCADE;
DROP TABLE IF EXISTS "IntegrationReference" CASCADE;
DROP TABLE IF EXISTS "CaseAttachment" CASCADE;
DROP TABLE IF EXISTS "CaseTransitionHistory" CASCADE;
DROP TABLE IF EXISTS "CaseForm" CASCADE;
DROP TABLE IF EXISTS "AuditLog" CASCADE;
DROP TABLE IF EXISTS "WorkflowRule" CASCADE;
DROP TABLE IF EXISTS "WorkflowProfile" CASCADE;
DROP TABLE IF EXISTS "OutboxMessage" CASCADE;
DROP TABLE IF EXISTS "ExternalEvent" CASCADE;
DROP TABLE IF EXISTS "WorkItem" CASCADE;
DROP TABLE IF EXISTS "Case" CASCADE;

CREATE TABLE "Case"
(
    "CaseId" uuid PRIMARY KEY,
    "HospitalId" varchar(32) NOT NULL,
    "SiteId" varchar(32) NOT NULL,
    "DepartmentId" varchar(32) NOT NULL,
    "PatientId" varchar(64) NULL,
    "AccessionNumber" varchar(64) NOT NULL,
    "CurrentStatus" integer NOT NULL,
    "StatusVersion" integer NOT NULL DEFAULT 0,
    "CtStudyInstanceUid" varchar(128) NULL,
    "CtWadoRsUrl" varchar(512) NULL,
    "PvMedJobId" varchar(128) NULL,
    "RtStructSeriesInstanceUid" varchar(128) NULL,
    "Notes" text NULL,
    "CurrentPlannerUserId" varchar(128) NULL,
    "CurrentReviewerUserId" varchar(128) NULL,
    "CurrentPlanVersionNo" integer NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL
);

CREATE UNIQUE INDEX "UX_Case_Hospital_Site_Department_Accession"
    ON "Case" ("HospitalId", "SiteId", "DepartmentId", "AccessionNumber");

CREATE INDEX "IX_Case_CurrentStatus"
    ON "Case" ("CurrentStatus");

CREATE TABLE "CaseForm"
(
    "FormId" uuid PRIMARY KEY,
    "CaseId" uuid NOT NULL,
    "FormType" varchar(64) NOT NULL,
    "FormVersion" integer NOT NULL,
    "Status" varchar(32) NOT NULL,
    "PayloadJson" text NOT NULL,
    "SubmittedBy" varchar(128) NULL,
    "SubmittedAt" timestamptz NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL,
    CONSTRAINT "FK_CaseForm_Case_CaseId" FOREIGN KEY ("CaseId") REFERENCES "Case"("CaseId") ON DELETE CASCADE
);

CREATE INDEX "IX_CaseForm_CaseId_FormType"
    ON "CaseForm" ("CaseId", "FormType");

CREATE TABLE "WorkItem"
(
    "WorkItemId" uuid PRIMARY KEY,
    "CaseId" uuid NOT NULL,
    "SequenceNo" integer NULL,
    "ParentWorkItemId" uuid NULL,
    "Type" varchar(64) NOT NULL,
    "Status" integer NOT NULL,
    "WorkItemGroup" varchar(64) NULL,
    "AssignedRole" varchar(64) NOT NULL,
    "AssignedUserId" varchar(128) NULL,
    "DueAt" timestamptz NULL,
    "SlaMinutes" integer NULL,
    "ExternalCorrelationId" varchar(128) NULL,
    "ResultCode" varchar(64) NULL,
    "CompletedAt" timestamptz NULL,
    "CompletedBy" varchar(128) NULL,
    "FormId" uuid NULL,
    "RequiresDifferentUserFrom" uuid NULL,
    "RetryCount" integer NOT NULL DEFAULT 0,
    "Remarks" text NULL,
    "PayloadJson" text NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL,
    CONSTRAINT "FK_WorkItem_Case_CaseId" FOREIGN KEY ("CaseId") REFERENCES "Case"("CaseId") ON DELETE CASCADE,
    CONSTRAINT "FK_WorkItem_WorkItem_ParentWorkItemId" FOREIGN KEY ("ParentWorkItemId") REFERENCES "WorkItem"("WorkItemId") ON DELETE SET NULL,
    CONSTRAINT "FK_WorkItem_CaseForm_FormId" FOREIGN KEY ("FormId") REFERENCES "CaseForm"("FormId") ON DELETE SET NULL
);

CREATE INDEX "IX_WorkItem_CaseId"
    ON "WorkItem" ("CaseId");

CREATE INDEX "IX_WorkItem_ParentWorkItemId"
    ON "WorkItem" ("ParentWorkItemId");

CREATE INDEX "IX_WorkItem_FormId"
    ON "WorkItem" ("FormId");

CREATE INDEX "IX_WorkItem_AssignedRole_Status"
    ON "WorkItem" ("AssignedRole", "Status");

CREATE INDEX "IX_WorkItem_Status"
    ON "WorkItem" ("Status");

CREATE TABLE "ExternalEvent"
(
    "EventId" uuid PRIMARY KEY,
    "Source" varchar(64) NOT NULL,
    "Type" varchar(64) NOT NULL,
    "ExternalId" varchar(128) NOT NULL,
    "CaseCorrelationKey" varchar(128) NULL,
    "CaseId" uuid NULL,
    "PayloadJson" text NOT NULL,
    "ReceivedAt" timestamptz NOT NULL,
    "ProcessedAt" timestamptz NULL,
    "ProcessStatus" varchar(32) NOT NULL,
    "Error" varchar(2048) NULL,
    CONSTRAINT "FK_ExternalEvent_Case_CaseId" FOREIGN KEY ("CaseId") REFERENCES "Case"("CaseId") ON DELETE SET NULL
);

CREATE UNIQUE INDEX "UX_ExternalEvent_Source_Type_ExternalId"
    ON "ExternalEvent" ("Source", "Type", "ExternalId");

CREATE INDEX "IX_ExternalEvent_CaseId"
    ON "ExternalEvent" ("CaseId");

CREATE TABLE "OutboxMessage"
(
    "MessageId" uuid PRIMARY KEY,
    "CaseId" uuid NULL,
    "TargetSystem" varchar(64) NOT NULL,
    "Action" varchar(64) NOT NULL,
    "PayloadJson" text NOT NULL,
    "Status" integer NOT NULL,
    "RetryCount" integer NOT NULL,
    "NextRetryAt" timestamptz NULL,
    "CreatedAt" timestamptz NOT NULL,
    "LastTriedAt" timestamptz NULL,
    CONSTRAINT "FK_OutboxMessage_Case_CaseId" FOREIGN KEY ("CaseId") REFERENCES "Case"("CaseId") ON DELETE SET NULL
);

CREATE INDEX "IX_OutboxMessage_Status_NextRetryAt"
    ON "OutboxMessage" ("Status", "NextRetryAt");

CREATE TABLE "WorkflowProfile"
(
    "ProfileId" uuid PRIMARY KEY,
    "HospitalId" varchar(32) NULL,
    "SiteId" varchar(32) NULL,
    "DepartmentId" varchar(32) NULL,
    "Name" varchar(128) NOT NULL,
    "Version" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamptz NOT NULL
);

CREATE UNIQUE INDEX "UX_WorkflowProfile_Hospital_Site_Department_Version"
    ON "WorkflowProfile" ("HospitalId", "SiteId", "DepartmentId", "Version");

CREATE TABLE "WorkflowRule"
(
    "RuleId" uuid PRIMARY KEY,
    "ProfileId" uuid NOT NULL,
    "SlotCode" varchar(64) NOT NULL,
    "Priority" integer NOT NULL,
    "ConditionJson" text NULL,
    "ConfigJson" text NOT NULL,
    "IsEnabled" boolean NOT NULL,
    "EffectiveFrom" timestamptz NULL,
    "EffectiveTo" timestamptz NULL,
    CONSTRAINT "FK_WorkflowRule_WorkflowProfile_ProfileId" FOREIGN KEY ("ProfileId") REFERENCES "WorkflowProfile"("ProfileId") ON DELETE CASCADE
);

CREATE INDEX "IX_WorkflowRule_ProfileId_SlotCode_IsEnabled"
    ON "WorkflowRule" ("ProfileId", "SlotCode", "IsEnabled");

CREATE INDEX "IX_WorkflowRule_ProfileId_SlotCode_Priority"
    ON "WorkflowRule" ("ProfileId", "SlotCode", "Priority");

CREATE TABLE "AuditLog"
(
    "AuditId" uuid PRIMARY KEY,
    "CaseId" uuid NOT NULL,
    "ActorType" varchar(32) NOT NULL,
    "ActorId" varchar(128) NULL,
    "Action" varchar(64) NOT NULL,
    "FromStatus" integer NULL,
    "ToStatus" integer NULL,
    "SnapshotJson" text NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    CONSTRAINT "FK_AuditLog_Case_CaseId" FOREIGN KEY ("CaseId") REFERENCES "Case"("CaseId") ON DELETE CASCADE
);

CREATE INDEX "IX_AuditLog_CaseId_CreatedAt"
    ON "AuditLog" ("CaseId", "CreatedAt");

CREATE TABLE "CaseTransitionHistory"
(
    "TransitionId" uuid PRIMARY KEY,
    "CaseId" uuid NOT NULL,
    "FromStatus" varchar(64) NOT NULL,
    "ToStatus" varchar(64) NOT NULL,
    "TriggerType" varchar(64) NOT NULL,
    "TriggerName" varchar(128) NOT NULL,
    "TriggeredBy" varchar(128) NULL,
    "Reason" varchar(1024) NULL,
    "MetadataJson" text NULL,
    "CreatedAt" timestamptz NOT NULL,
    CONSTRAINT "FK_CaseTransitionHistory_Case_CaseId" FOREIGN KEY ("CaseId") REFERENCES "Case"("CaseId") ON DELETE CASCADE
);

CREATE INDEX "IX_CaseTransitionHistory_CaseId_CreatedAt"
    ON "CaseTransitionHistory" ("CaseId", "CreatedAt");

CREATE TABLE "CaseAttachment"
(
    "AttachmentId" uuid PRIMARY KEY,
    "CaseId" uuid NOT NULL,
    "Category" varchar(64) NOT NULL,
    "FileName" varchar(256) NOT NULL,
    "StoragePath" varchar(1024) NOT NULL,
    "SourceSystem" varchar(64) NULL,
    "UploadedBy" varchar(128) NULL,
    "UploadedAt" timestamptz NOT NULL,
    CONSTRAINT "FK_CaseAttachment_Case_CaseId" FOREIGN KEY ("CaseId") REFERENCES "Case"("CaseId") ON DELETE CASCADE
);

CREATE INDEX "IX_CaseAttachment_CaseId"
    ON "CaseAttachment" ("CaseId");

CREATE TABLE "IntegrationReference"
(
    "Id" uuid PRIMARY KEY,
    "CaseId" uuid NOT NULL,
    "SystemName" varchar(64) NOT NULL,
    "ExternalEntityType" varchar(64) NOT NULL,
    "ExternalId" varchar(128) NOT NULL,
    "ExternalStatus" varchar(64) NULL,
    "MetadataJson" text NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL,
    CONSTRAINT "FK_IntegrationReference_Case_CaseId" FOREIGN KEY ("CaseId") REFERENCES "Case"("CaseId") ON DELETE CASCADE
);

CREATE INDEX "IX_IntegrationReference_CaseId_SystemName"
    ON "IntegrationReference" ("CaseId", "SystemName");

CREATE TABLE "PlanVersion"
(
    "PlanVersionId" uuid PRIMARY KEY,
    "CaseId" uuid NOT NULL,
    "VersionNo" integer NOT NULL,
    "SourceSystem" varchar(64) NOT NULL,
    "Status" varchar(32) NOT NULL,
    "SummaryJson" text NULL,
    "CreatedAt" timestamptz NOT NULL,
    CONSTRAINT "FK_PlanVersion_Case_CaseId" FOREIGN KEY ("CaseId") REFERENCES "Case"("CaseId") ON DELETE CASCADE
);

CREATE INDEX "IX_PlanVersion_CaseId_VersionNo"
    ON "PlanVersion" ("CaseId", "VersionNo");

WITH seed AS (
        SELECT
                '11111111-1111-1111-1111-111111111111'::uuid AS global_profile_id,
                '22222222-2222-2222-2222-222222222222'::uuid AS department_profile_id,
                now() AS created_at
)
INSERT INTO "WorkflowProfile"
(
        "ProfileId", "HospitalId", "SiteId", "DepartmentId", "Name", "Version", "IsActive", "CreatedAt"
)
SELECT global_profile_id, NULL, NULL, NULL, 'Global Default Workflow', 1, true, created_at
FROM seed
UNION ALL
SELECT department_profile_id, 'HOSP001', 'SITE_A', 'RT', 'RT Department Workflow', 1, true, created_at
FROM seed;

WITH seed AS (
    SELECT
        '11111111-1111-1111-1111-111111111111'::uuid AS global_profile_id,
        '22222222-2222-2222-2222-222222222222'::uuid AS department_profile_id,
        now() AS created_at
)
INSERT INTO "WorkflowRule"
(
        "RuleId", "ProfileId", "SlotCode", "Priority", "ConditionJson", "ConfigJson", "IsEnabled", "EffectiveFrom", "EffectiveTo"
)
SELECT
        v."RuleId",
        seed.department_profile_id,
        v."SlotCode",
        v."Priority",
        v."ConditionJson",
        v."ConfigJson",
        true,
        seed.created_at,
        NULL
FROM seed
CROSS JOIN (
        VALUES
        (
                '30000000-0000-0000-0000-000000000001'::uuid,
                'S1_CONTOURING_STRATEGY',
                1,
                NULL,
                $$
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
                        "manualWorkItemRole": "Doctor"
                    }
                }
                $$
        ),
        (
                '30000000-0000-0000-0000-000000000002'::uuid,
                'S2_CONTOUR_REVIEW_POLICY',
                1,
                NULL,
                $$
                {
                    "reviewMode": "Single",
                    "allowSecondReview": false,
                    "onReject": {
                        "targetStatus": "ContourReworkRequired",
                        "createReworkWorkItem": true,
                        "reworkWorkItemRole": "Doctor"
                    },
                    "timeoutHours": 24
                }
                $$
        ),
        (
                '30000000-0000-0000-0000-000000000003'::uuid,
                'S3_PLAN_DISPATCH',
                1,
                NULL,
                $$
                {
                    "dispatchMode": "AutoAssignByRole",
                    "targetRole": "Dosimetrist",
                    "allowManualClaim": true,
                    "slaMinutes": 240,
                    "escalation": {
                        "enabled": true,
                        "afterMinutes": 180,
                        "escalateToRole": "ChiefDoctor"
                    }
                }
                $$
        ),
        (
                '30000000-0000-0000-0000-000000000004'::uuid,
                'S4_PLAN_REREVIEW_POLICY',
                1,
                NULL,
                $$
                {
                    "enabled": true,
                    "trigger": {
                        "riskLevelIn": ["High"],
                        "doseDeltaPercentGte": 5
                    },
                    "reviewRole": "SeniorPhysicist",
                    "onRejectBackTo": "PlanningInProgress"
                }
                $$
        ),
        (
                '30000000-0000-0000-0000-000000000005'::uuid,
                'S5_PLAN_DOUBLE_CHECK',
                1,
                NULL,
                $$
                {
                    "enabled": true,
                    "workItemRole": "QAReviewer",
                    "requiresDifferentUserFrom": "PlanQA",
                    "onFailBackTo": "PlanQAInProgress",
                    "maxRetry": 1
                }
                $$
        ),
        (
                '30000000-0000-0000-0000-000000000006'::uuid,
                'S6_QUEUE_AND_CANCEL_POLICY',
                1,
                NULL,
                $$
                {
                    "queueMode": "MsqDriven",
                    "allowCancel": true,
                    "cancelAllowedBeforeStatus": "Treating",
                    "requireCancelReason": true,
                    "onCancel": {
                        "closeOpenWorkItems": true,
                        "createAudit": true,
                        "finalStatus": "Cancelled"
                    }
                }
                $$
        ),
        (
                '30000000-0000-0000-0000-000000000007'::uuid,
                'S7_TREATMENT_COMPLETION_POLICY',
                1,
                NULL,
                $$
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
                $$
        ),
        (
                '30000000-0000-0000-0000-000000000008'::uuid,
                'S8_EXCEPTION_HANDLING_POLICY',
                1,
                NULL,
                $$
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
                $$
        )
) AS v("RuleId", "SlotCode", "Priority", "ConditionJson", "ConfigJson");

WITH seed AS (
    SELECT
        '11111111-1111-1111-1111-111111111111'::uuid AS global_profile_id,
        '22222222-2222-2222-2222-222222222222'::uuid AS department_profile_id,
        now() AS created_at
)
INSERT INTO "WorkflowRule"
(
        "RuleId", "ProfileId", "SlotCode", "Priority", "ConditionJson", "ConfigJson", "IsEnabled", "EffectiveFrom", "EffectiveTo"
)
SELECT
        v."RuleId",
        seed.global_profile_id,
        v."SlotCode",
        v."Priority",
        v."ConditionJson",
        v."ConfigJson",
        true,
        seed.created_at,
        NULL
FROM seed
CROSS JOIN (
        VALUES
        (
                '30000000-0000-0000-0000-000000000101'::uuid,
                'S1_CONTOURING_STRATEGY',
                1,
                NULL,
                $$
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
                        "manualWorkItemRole": "Doctor"
                    }
                }
                $$
        ),
        (
                '30000000-0000-0000-0000-000000000102'::uuid,
                'S2_CONTOUR_REVIEW_POLICY',
                1,
                NULL,
                $$
                {
                    "reviewMode": "Single",
                    "allowSecondReview": false,
                    "onReject": {
                        "targetStatus": "ContourReworkRequired",
                        "createReworkWorkItem": true,
                        "reworkWorkItemRole": "Doctor"
                    },
                    "timeoutHours": 24
                }
                $$
        ),
        (
                '30000000-0000-0000-0000-000000000103'::uuid,
                'S3_PLAN_DISPATCH',
                1,
                NULL,
                $$
                {
                    "dispatchMode": "AutoAssignByRole",
                    "targetRole": "Dosimetrist",
                    "allowManualClaim": true,
                    "slaMinutes": 240,
                    "escalation": {
                        "enabled": false,
                        "afterMinutes": 180,
                        "escalateToRole": "ChiefDoctor"
                    }
                }
                $$
        ),
        (
                '30000000-0000-0000-0000-000000000104'::uuid,
                'S4_PLAN_REREVIEW_POLICY',
                1,
                NULL,
                $$
                {
                    "enabled": false,
                    "trigger": {
                        "riskLevelIn": [],
                        "doseDeltaPercentGte": null
                    },
                    "reviewRole": "SeniorPhysicist",
                    "onRejectBackTo": "PlanningInProgress"
                }
                $$
        ),
        (
                '30000000-0000-0000-0000-000000000105'::uuid,
                'S5_PLAN_DOUBLE_CHECK',
                1,
                NULL,
                $$
                {
                    "enabled": false,
                    "workItemRole": "QAReviewer",
                    "requiresDifferentUserFrom": "PlanQA",
                    "onFailBackTo": "PlanQAInProgress",
                    "maxRetry": 1
                }
                $$
        ),
        (
                '30000000-0000-0000-0000-000000000106'::uuid,
                'S6_QUEUE_AND_CANCEL_POLICY',
                1,
                NULL,
                $$
                {
                    "queueMode": "MsqDriven",
                    "allowCancel": true,
                    "cancelAllowedBeforeStatus": "Treating",
                    "requireCancelReason": true,
                    "onCancel": {
                        "closeOpenWorkItems": true,
                        "createAudit": true,
                        "finalStatus": "Cancelled"
                    }
                }
                $$
        ),
        (
                '30000000-0000-0000-0000-000000000107'::uuid,
                'S7_TREATMENT_COMPLETION_POLICY',
                1,
                NULL,
                $$
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
                $$
        ),
        (
                '30000000-0000-0000-0000-000000000108'::uuid,
                'S8_EXCEPTION_HANDLING_POLICY',
                1,
                NULL,
                $$
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
                $$
        )
) AS v("RuleId", "SlotCode", "Priority", "ConditionJson", "ConfigJson");

DO $$
DECLARE
    missing_count integer;
    missing_details text;
BEGIN
    SELECT COUNT(*)
    INTO missing_count
    FROM (
        SELECT
            p."ProfileId",
            s."SlotCode"
        FROM "WorkflowProfile" p
        CROSS JOIN (
            VALUES
                ('S1_CONTOURING_STRATEGY'),
                ('S2_CONTOUR_REVIEW_POLICY'),
                ('S3_PLAN_DISPATCH'),
                ('S4_PLAN_REREVIEW_POLICY'),
                ('S5_PLAN_DOUBLE_CHECK'),
                ('S6_QUEUE_AND_CANCEL_POLICY'),
                ('S7_TREATMENT_COMPLETION_POLICY'),
                ('S8_EXCEPTION_HANDLING_POLICY')
        ) AS s("SlotCode")
        LEFT JOIN "WorkflowRule" r
            ON r."ProfileId" = p."ProfileId"
           AND r."SlotCode" = s."SlotCode"
           AND r."IsEnabled" = true
        WHERE p."ProfileId" IN (
            '11111111-1111-1111-1111-111111111111'::uuid,
            '22222222-2222-2222-2222-222222222222'::uuid
        )
          AND r."RuleId" IS NULL
    ) missing;

    SELECT string_agg(
        '(' || missing."ProfileId"::text || ', ' || missing."SlotCode" || ')',
        ', '
        ORDER BY missing."ProfileId", missing."SlotCode")
    INTO missing_details
    FROM (
        SELECT
            p."ProfileId",
            s."SlotCode"
        FROM "WorkflowProfile" p
        CROSS JOIN (
            VALUES
                ('S1_CONTOURING_STRATEGY'),
                ('S2_CONTOUR_REVIEW_POLICY'),
                ('S3_PLAN_DISPATCH'),
                ('S4_PLAN_REREVIEW_POLICY'),
                ('S5_PLAN_DOUBLE_CHECK'),
                ('S6_QUEUE_AND_CANCEL_POLICY'),
                ('S7_TREATMENT_COMPLETION_POLICY'),
                ('S8_EXCEPTION_HANDLING_POLICY')
        ) AS s("SlotCode")
        LEFT JOIN "WorkflowRule" r
            ON r."ProfileId" = p."ProfileId"
           AND r."SlotCode" = s."SlotCode"
           AND r."IsEnabled" = true
        WHERE p."ProfileId" IN (
            '11111111-1111-1111-1111-111111111111'::uuid,
            '22222222-2222-2222-2222-222222222222'::uuid
        )
          AND r."RuleId" IS NULL
    ) missing;

    IF missing_count > 0 THEN
        RAISE EXCEPTION 'Workflow seed validation failed: % required slot rules are missing. Missing pairs: %', missing_count, COALESCE(missing_details, '<none>');
    END IF;
END $$;

COMMIT;
