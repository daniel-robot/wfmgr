下面给你一份可直接落地的 **TransitionMatrix + CompensationMatrix 开发规格表**。
你可以把它：

* 放进设计文档
* 交给 Codex 生成代码
* 交给开发同事实现状态机
* 作为测试用例来源

---

# 1. TransitionMatrix 设计说明

建议每条迁移至少包含这些字段：

* `TransitionCode`
* `FromStatus`
* `ToStatus`
* `TriggerName`
* `TriggerType`
* `RequiredRole`
* `GateChecks`
* `OnSuccessActions`
* `OnFailureActions`
* `CreateWorkItems`
* `ConfigSlot`

---

# 2. TransitionMatrix（主干版）

## 2.1 申请与模拟阶段

| TransitionCode | FromStatus                           | ToStatus      | TriggerName             | TriggerType | RequiredRole      | GateChecks                  | OnSuccessActions                        | OnFailureActions      | CreateWorkItems                      | ConfigSlot |
| -------------- | ------------------------------------ | ------------- | ----------------------- | ----------- | ----------------- | --------------------------- | --------------------------------------- | --------------------- | ------------------------------------ | ---------- |
| SIM-001        | Draft                                | Submitted     | SubmitSimulationRequest | User        | Doctor            | SimulationRequestForm valid | Save form, audit, transition history    | reject transition     | SimulationSchedule, SimulationRecord | -          |
| SIM-002        | Submitted                            | SimScheduled  | ScheduleSimulation      | User        | SimTech/Scheduler | active case, not cancelled  | save schedule info                      | stay in Submitted     | update SimulationSchedule            | -          |
| SIM-003        | SimScheduled                         | SimInProgress | StartSimulation         | User        | SimTech           | schedule exists             | audit                                   | stay in SimScheduled  | -                                    | -          |
| SIM-004        | SimInProgress                        | SimCompleted  | SubmitSimulationRecord  | User        | SimTech           | SimulationRecordForm valid  | save form, complete SimulationRecord    | stay in SimInProgress | ImageValidation                      | -          |
| SIM-005        | Submitted/SimScheduled/SimInProgress | Cancelled     | CancelCase              | User        | Doctor/Admin      | treatment not started       | save CancellationForm, close open tasks | reject cancel         | cancel open work items               | S8         |

---

## 2.2 影像与勾画启动阶段

| TransitionCode | FromStatus      | ToStatus             | TriggerName               | TriggerType          | RequiredRole | GateChecks                                         | OnSuccessActions                                     | OnFailureActions  | CreateWorkItems                             | ConfigSlot |
| -------------- | --------------- | -------------------- | ------------------------- | -------------------- | ------------ | -------------------------------------------------- | ---------------------------------------------------- | ----------------- | ------------------------------------------- | ---------- |
| IMG-001        | SimCompleted    | ImageStored          | ReceiveCtImageStoredEvent | ExternalEvent        | System       | case resolved by correlation key, image refs valid | save ExternalEvent, image refs, IntegrationReference | mark event failed | ImageValidation if manual review needed     | -          |
| IMG-002        | ImageStored     | ImageForwarding      | StartImageForwarding      | System               | System       | image accessible                                   | create Outbox SendImagesToContourTool                | retry outbox      | ImageForwardToContourTool if retry exceeded | S1         |
| IMG-003        | ImageForwarding | ContouringInProgress | ContourToolAccepted       | ExternalEvent/System | System       | external accept or delivery confirmed              | save external job ref                                | manual resend     | AutoContourMonitor                          | S1         |

---

## 2.3 勾画阶段

| TransitionCode | FromStatus            | ToStatus              | TriggerName                | TriggerType   | RequiredRole              | GateChecks                   | OnSuccessActions                  | OnFailureActions    | CreateWorkItems                     | ConfigSlot |
| -------------- | --------------------- | --------------------- | -------------------------- | ------------- | ------------------------- | ---------------------------- | --------------------------------- | ------------------- | ----------------------------------- | ---------- |
| CON-001        | ContouringInProgress  | ContouringInProgress  | AutoContourProgressUpdated | ExternalEvent | System                    | event idempotent             | update progress, audit            | ignore duplicate    | update AutoContourMonitor           | -          |
| CON-002        | ContouringInProgress  | ContoursReady         | AutoContourCompleted       | ExternalEvent | System                    | contour result refs valid    | save RTStruct refs, close monitor | create rework       | ContourReview / ContourSecondReview | S2         |
| CON-003        | ContouringInProgress  | ContourReworkRequired | AutoContourFailed          | ExternalEvent | System                    | event valid                  | audit failure                     | retry if configured | ManualContouring                    | S1/S8      |
| CON-004        | ContourReworkRequired | ContouringInProgress  | RestartContouring          | User/System   | Doctor/System             | retry allowed                | create new Outbox                 | stay in rework      | AutoContourMonitor                  | S1         |
| CON-005        | ContourReworkRequired | ContoursReady         | SubmitManualContourResult  | User          | Doctor/ThirdPartyOperator | manual contour payload valid | save contour refs                 | stay in rework      | ContourReview / ContourSecondReview | S2         |

---

## 2.4 勾画审核阶段

| TransitionCode | FromStatus          | ToStatus             | TriggerName        | TriggerType | RequiredRole       | GateChecks                | OnSuccessActions         | OnFailureActions  | CreateWorkItems                             | ConfigSlot |
| -------------- | ------------------- | -------------------- | ------------------ | ----------- | ------------------ | ------------------------- | ------------------------ | ----------------- | ------------------------------------------- | ---------- |
| REV-001        | ContoursReady       | ContoursUnderReview  | StartContourReview | System      | System             | contour result exists     | create review tasks      | -                 | ContourReview, optional ContourSecondReview | S2         |
| REV-002        | ContoursUnderReview | PlanningPending      | ApproveContours    | User        | Doctor/ChiefDoctor | min approvals reached     | complete review tasks    | reject transition | PlanAssignment                              | S2         |
| REV-003        | ContoursUnderReview | ContoursRejected     | RejectContours     | User        | Doctor/ChiefDoctor | rejection reason required | save review form         | reject transition | ContourRework                               | S2         |
| REV-004        | ContoursRejected    | ContouringInProgress | ResubmitContours   | User/System | Doctor/System      | revised contour exists    | clear stale review tasks | stay rejected     | AutoContourMonitor or manual rework         | S8         |

---

## 2.5 计划阶段

| TransitionCode | FromStatus         | ToStatus           | TriggerName       | TriggerType        | RequiredRole          | GateChecks                | OnSuccessActions               | OnFailureActions  | CreateWorkItems       | ConfigSlot |
| -------------- | ------------------ | ------------------ | ----------------- | ------------------ | --------------------- | ------------------------- | ------------------------------ | ----------------- | --------------------- | ---------- |
| PLN-001        | PlanningPending    | PlanningAssigned   | AssignPlanner     | User/System        | Scheduler/System      | assignee exists           | save planner info              | stay pending      | PlanDesign            | S3         |
| PLN-002        | PlanningAssigned   | PlanningInProgress | AcceptPlanTask    | User               | Dosimetrist/Physicist | task assigned             | mark task in progress          | stay assigned     | -                     | S3         |
| PLN-003        | PlanningInProgress | PlanReady          | SubmitPlan        | User/ExternalEvent | Dosimetrist/Monaco    | plan payload valid        | create PlanVersion             | reject submit     | PlanEvaluation        | -          |
| PLN-004        | PlanReady          | PlanUnderReview    | StartPlanReview   | System/User        | System/Physicist      | plan version exists       | create evaluation task         | -                 | PlanEvaluation        | -          |
| PLN-005        | PlanUnderReview    | PlanningInProgress | RejectPlan        | User               | Physicist/Doctor      | rejection reason required | increment plan version context | stay review       | PlanDesign            | -          |
| PLN-006        | PlanUnderReview    | PlanReviewed       | ApprovePlanReview | User               | Physicist/Doctor      | evaluation approved       | audit                          | reject transition | optional PlanReReview | S4         |

---

## 2.6 复审与处方阶段

| TransitionCode | FromStatus             | ToStatus               | TriggerName                 | TriggerType          | RequiredRole                | GateChecks                   | OnSuccessActions                   | OnFailureActions  | CreateWorkItems  | ConfigSlot |
| -------------- | ---------------------- | ---------------------- | --------------------------- | -------------------- | --------------------------- | ---------------------------- | ---------------------------------- | ----------------- | ---------------- | ---------- |
| RX-001         | PlanReviewed           | PlanReReviewOptional   | StartPlanReReview           | System               | System                      | S4 enabled                   | create rereview task               | skip if disabled  | PlanReReview     | S4         |
| RX-002         | PlanReviewed           | PrescriptionGenerating | StartPrescriptionGeneration | System               | System                      | no rereview required         | create Outbox GeneratePrescription | retry/manual sync | PrescriptionSync | S4         |
| RX-003         | PlanReReviewOptional   | PrescriptionGenerating | ApprovePlanReReview         | User                 | ChiefDoctor/SeniorPhysicist | rereview approved            | create Outbox GeneratePrescription | reject transition | PrescriptionSync | S4         |
| RX-004         | PlanReReviewOptional   | PlanningInProgress     | RejectPlanReReview          | User                 | ChiefDoctor/SeniorPhysicist | reason required              | plan back to rework                | stay rereview     | PlanDesign       | S4         |
| RX-005         | PrescriptionGenerating | PrescriptionReady      | PrescriptionGenerated       | ExternalEvent/System | System                      | prescription reference valid | save IntegrationReference          | retry/manual sync | PlanQA           | -          |
| RX-006         | PrescriptionGenerating | PrescriptionSyncFailed | PrescriptionSyncFailed      | ExternalEvent/System | System                      | failure event valid          | audit failure                      | retry later       | PrescriptionSync | S8         |
| RX-007         | PrescriptionSyncFailed | PrescriptionGenerating | RetryPrescriptionSync       | User/System          | Physicist/System            | retry allowed                | create new Outbox                  | stay failed       | PrescriptionSync | S8         |

---

## 2.7 QA 与复核阶段

| TransitionCode | FromStatus              | ToStatus                | TriggerName        | TriggerType | RequiredRole         | GateChecks                  | OnSuccessActions         | OnFailureActions  | CreateWorkItems                             | ConfigSlot |
| -------------- | ----------------------- | ----------------------- | ------------------ | ----------- | -------------------- | --------------------------- | ------------------------ | ----------------- | ------------------------------------------- | ---------- |
| QA-001         | PrescriptionReady       | PlanQAInProgress        | StartQA            | System      | System               | plan + prescription present | create QA task           | -                 | PlanQA                                      | -          |
| QA-002         | PlanQAInProgress        | PlanQAApproved          | ApproveQA          | User        | Physicist/QAReviewer | QA form valid               | save QA report           | reject transition | optional PlanDoubleCheck                    | S5         |
| QA-003         | PlanQAInProgress        | PlanQAFailed            | RejectQA           | User        | Physicist/QAReviewer | failure reason required     | save QA failure          | reject transition | PlanDesign or PlanReReview depending policy | S8         |
| QA-004         | PlanQAFailed            | PlanningInProgress      | ReworkAfterQA      | User/System | Physicist/System     | rework decision made        | reopen planning path     | stay failed       | PlanDesign                                  | S8         |
| QA-005         | PlanQAApproved          | PlanDoubleCheckOptional | StartDoubleCheck   | System      | System               | S5 enabled                  | create double check task | skip if disabled  | PlanDoubleCheck                             | S5         |
| QA-006         | PlanQAApproved          | ReadyForScheduling      | SkipDoubleCheck    | System      | System               | S5 disabled                 | audit                    | -                 | ScheduleSync                                | S5         |
| QA-007         | PlanDoubleCheckOptional | ReadyForScheduling      | ApproveDoubleCheck | User        | SeniorPhysicist      | double check approved       | complete task            | reject transition | ScheduleSync                                | S5         |
| QA-008         | PlanDoubleCheckOptional | PlanningInProgress      | RejectDoubleCheck  | User        | SeniorPhysicist      | reason required             | reopen planning          | stay double check | PlanDesign                                  | S5         |

---

## 2.8 排程、医嘱与治疗阶段

| TransitionCode | FromStatus           | ToStatus             | TriggerName                | TriggerType          | RequiredRole            | GateChecks                   | OnSuccessActions                 | OnFailureActions     | CreateWorkItems            | ConfigSlot |
| -------------- | -------------------- | -------------------- | -------------------------- | -------------------- | ----------------------- | ---------------------------- | -------------------------------- | -------------------- | -------------------------- | ---------- |
| TRT-001        | ReadyForScheduling   | SchedulingInProgress | StartScheduleSync          | System               | System                  | case released for schedule   | start schedule watch             | retry/manual         | ScheduleSync               | S6         |
| TRT-002        | SchedulingInProgress | Scheduled            | ScheduleSynced             | ExternalEvent        | System                  | schedule payload valid       | save schedule ref                | retry/manual sync    | TreatmentOrder             | S6         |
| TRT-003        | Scheduled            | OrderPending         | PrepareTreatmentOrder      | System               | System                  | schedule exists              | create order draft               | -                    | TreatmentOrder             | -          |
| TRT-004        | OrderPending         | OrderSubmitted       | SubmitTreatmentOrder       | User                 | Doctor                  | TreatmentOrderForm valid     | save order form                  | stay pending         | QueueCall                  | -          |
| TRT-005        | OrderSubmitted       | QueuePending         | QueueCreated               | ExternalEvent/System | System                  | queue or appointment valid   | save queue ref                   | retry/local fallback | QueueCall                  | S6         |
| TRT-006        | QueuePending         | Treating             | TreatmentStarted           | ExternalEvent        | System                  | treatment start event valid  | create monitor                   | reject event         | TreatmentMonitor           | -          |
| TRT-007        | Treating             | Treating             | TreatmentFractionCompleted | ExternalEvent        | System                  | fraction data valid          | update progress                  | ignore duplicate     | TreatmentMonitor           | S7         |
| TRT-008        | Treating             | TreatmentPaused      | PauseTreatment             | User/ExternalEvent   | Therapist/System        | pause reason provided        | audit pause                      | reject transition    | TreatmentExceptionHandling | S8         |
| TRT-009        | TreatmentPaused      | Treating             | ResumeTreatment            | User/ExternalEvent   | Therapist/System        | resume allowed               | close exception task if resolved | stay paused          | TreatmentMonitor           | S8         |
| TRT-010        | Treating             | TreatmentInterrupted | InterruptTreatment         | User/ExternalEvent   | Therapist/Doctor/System | interruption reason required | audit interruption               | reject transition    | TreatmentExceptionHandling | S8         |
| TRT-011        | TreatmentInterrupted | Treating             | ResumeAfterInterruption    | User                 | Doctor/Therapist        | medical approval exists      | resume monitor                   | stay interrupted     | TreatmentMonitor           | S8         |
| TRT-012        | Treating             | TreatmentCompleted   | CompleteTreatmentCourse    | ExternalEvent/System | System                  | S7 completion rule satisfied | create post review               | stay treating        | PostTreatmentReview        | S7         |

---

## 2.9 治疗后与归档阶段

| TransitionCode | FromStatus                 | ToStatus                   | TriggerName               | TriggerType  | RequiredRole | GateChecks                                 | OnSuccessActions    | OnFailureActions | CreateWorkItems                | ConfigSlot |
| -------------- | -------------------------- | -------------------------- | ------------------------- | ------------ | ------------ | ------------------------------------------ | ------------------- | ---------------- | ------------------------------ | ---------- |
| POST-001       | TreatmentCompleted         | PostTreatmentReviewPending | StartPostTreatmentReview  | System       | System       | treatment completed                        | create review task  | -                | PostTreatmentReview            | -          |
| POST-002       | PostTreatmentReviewPending | PostTreatmentReviewed      | SubmitPostTreatmentReview | User         | Doctor       | PostTreatmentReviewForm valid              | save form           | stay pending     | ArchiveReview                  | -          |
| POST-003       | PostTreatmentReviewed      | Archived                   | ArchiveCase               | System/Admin | System/Admin | no blocking tasks, required forms complete | mark case read-only | reject archive   | close or cancel residual tasks | -          |

---

# 3. CompensationMatrix

## 3.1 字段建议

* `CompensationCode`
* `FailedStep`
* `FailureCondition`
* `CompensationAction`
* `TargetStatus`
* `CreateWorkItem`
* `RetryPolicy`
* `ManualInterventionRequired`

---

## 3.2 CompensationMatrix（核心版）

| CompensationCode | FailedStep                         | FailureCondition               | CompensationAction                                     | TargetStatus                               | CreateWorkItem             | RetryPolicy            | ManualInterventionRequired |
| ---------------- | ---------------------------------- | ------------------------------ | ------------------------------------------------------ | ------------------------------------------ | -------------------------- | ---------------------- | -------------------------- |
| CMP-001          | IMG-002 SendImagesToContourTool    | outbox send failed             | retry send, then fallback to manual forward            | ImageStored or ImageForwarding             | ImageForwardToContourTool  | exponential backoff    | Yes after retry limit      |
| CMP-002          | IMG-003 ContourToolAccepted        | external tool not accepting    | keep case safe, allow manual resend                    | ImageStored                                | ImageForwardToContourTool  | limited retry          | Yes                        |
| CMP-003          | CON-002 AutoContourCompleted       | contour result invalid/corrupt | request manual contouring                              | ContourReworkRequired                      | ManualContouring           | none                   | Yes                        |
| CMP-004          | CON-003 AutoContourFailed          | PvMed/3rd-party failed         | create manual contour rework                           | ContourReworkRequired                      | ManualContouring           | optional retry         | Yes                        |
| CMP-005          | REV-003 RejectContours             | contour review rejected        | route back for contour rework                          | ContoursRejected                           | ContourRework              | no retry               | Yes                        |
| CMP-006          | PLN-005 RejectPlan                 | plan evaluation failed         | reopen planning and create new plan task               | PlanningInProgress                         | PlanDesign                 | no retry               | Yes                        |
| CMP-007          | RX-004 RejectPlanReReview          | rereview failed                | reopen planning with new version                       | PlanningInProgress                         | PlanDesign                 | no retry               | Yes                        |
| CMP-008          | RX-006 PrescriptionSyncFailed      | prescription sync failed       | create manual sync task                                | PrescriptionSyncFailed                     | PrescriptionSync           | retry supported        | Yes                        |
| CMP-009          | QA-003 RejectQA                    | QA failed                      | return to planning for rework                          | PlanQAFailed then PlanningInProgress       | PlanDesign                 | no retry               | Yes                        |
| CMP-010          | QA-008 RejectDoubleCheck           | double check failed            | reopen planning path                                   | PlanningInProgress                         | PlanDesign                 | no retry               | Yes                        |
| CMP-011          | TRT-001 StartScheduleSync          | schedule sync timeout/failure  | retry sync, then manual scheduling sync                | ReadyForScheduling or SchedulingInProgress | ScheduleSync               | retry supported        | Yes                        |
| CMP-012          | TRT-004 SubmitTreatmentOrder       | order submit validation failed | remain pending until corrected                         | OrderPending                               | TreatmentOrder             | no auto retry          | Yes                        |
| CMP-013          | TRT-005 QueueCreated               | queue integration failed       | keep order submitted, allow local/manual queue step    | OrderSubmitted                             | QueueCall                  | retry optional         | Yes                        |
| CMP-014          | TRT-008 PauseTreatment             | paused case not resumed in SLA | create escalation handling                             | TreatmentPaused                            | TreatmentExceptionHandling | timer-based escalation | Yes                        |
| CMP-015          | TRT-010 InterruptTreatment         | interruption occurs            | open exception handling and require clinician decision | TreatmentInterrupted                       | TreatmentExceptionHandling | no auto retry          | Yes                        |
| CMP-016          | TRT-012 CompleteTreatmentCourse    | completion data incomplete     | remain in treating and continue monitoring             | Treating                                   | TreatmentMonitor           | polling/retry          | No                         |
| CMP-017          | POST-002 SubmitPostTreatmentReview | form incomplete                | keep pending                                           | PostTreatmentReviewPending                 | PostTreatmentReview        | none                   | Yes                        |
| CMP-018          | POST-003 ArchiveCase               | blocking task exists           | reject archive until resolved                          | PostTreatmentReviewed                      | ArchiveReview              | none                   | Yes                        |
| CMP-019          | SIM-005 CancelCase                 | cancel not medically allowed   | reject cancel                                          | current status unchanged                   | none                       | none                   | Yes                        |
| CMP-020          | Any external event step            | duplicate event                | ignore duplicate safely                                | current status unchanged                   | none                       | none                   | No                         |

---

# 4. 建议给代码里的结构

你可以让 Codex 按下面思路实现：

## 4.1 TransitionDefinition

```csharp
public sealed class TransitionDefinition
{
    public string Code { get; init; } = default!;
    public CaseStatus[] FromStatuses { get; init; } = Array.Empty<CaseStatus>();
    public CaseStatus ToStatus { get; init; }
    public string TriggerName { get; init; } = default!;
    public TriggerType TriggerType { get; init; }
    public string? RequiredRole { get; init; }
    public string[] GateChecks { get; init; } = Array.Empty<string>();
    public string[] SuccessActions { get; init; } = Array.Empty<string>();
    public string[] FailureActions { get; init; } = Array.Empty<string>();
    public string[] WorkItemsToCreate { get; init; } = Array.Empty<string>();
    public string? ConfigSlot { get; init; }
}
```

## 4.2 CompensationDefinition

```csharp
public sealed class CompensationDefinition
{
    public string Code { get; init; } = default!;
    public string FailedStepCode { get; init; } = default!;
    public string FailureCondition { get; init; } = default!;
    public string CompensationAction { get; init; } = default!;
    public CaseStatus? TargetStatus { get; init; }
    public string? WorkItemToCreate { get; init; }
    public bool ManualInterventionRequired { get; init; }
    public string? RetryPolicy { get; init; }
}
```

---

# 5. 最推荐的下一步

现在最适合继续做的是把这两张表变成 **Codex Prompt**，让它自动改你现有项目，去实现：

* `CaseTransitionService`
* `GateValidationService`
* `CompensationService`
* `TransitionHistory` 写入
* `WorkItem` 自动创建逻辑

我可以下一条直接帮你生成一组 **基于这份 Matrix 的 Codex Prompt**。
