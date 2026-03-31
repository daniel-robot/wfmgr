# wfmgr 放疗工作流平台详细设计文档

## 1. 文档概述

### 1.1 文档目的

本文档用于定义 **wfmgr（Workflow Manager）放疗工作流平台** 的详细设计方案，指导后端、前端、集成、数据库与测试工作的落地实施。

wfmgr 的目标是为放疗行业提供一个可配置、可审计、可集成、可扩展的工作流控制平台，用于贯穿患者从模拟定位申请、图像处理、勾画、计划设计、QA、排程、治疗执行到治疗后评估的全流程闭环。

### 1.2 设计目标

* 支持放疗业务完整主干流程。
* 支持医院级、院区级、科室级流程差异化配置。
* 支持人与系统协同的混合工作流。
* 支持外部系统集成：CT/影像系统、PvMed、Monaco、MSQ、第三方勾画工具。
* 支持高审计要求与医疗场景下的可追溯性。
* 支持失败重试、异常回退、人工补救。
* 基于 .NET 8、ASP.NET Core、EF Core、SQL Server 实现。

### 1.3 范围

本文档覆盖：

* 系统架构设计
* 领域模型设计
* 状态机设计
* 任务模型设计
* 配置模型设计
* 外部集成设计
* 数据库设计
* API 设计
* 前端测试控制台设计
* 安全、审计、可靠性设计
* 开发实施建议

不包含：

* 最终 UI 视觉稿
* 第三方系统的具体私有协议细节
* 部署环境专属网络拓扑图

---

## 2. 业务背景与需求摘要

### 2.1 业务背景

放疗流程涉及多角色、多系统、多阶段协作，现有流程通常分散在多个系统中，缺少统一工作流控制与审计闭环。wfmgr 作为流程控制中台，负责：

* 统一病例主状态管理
* 统一任务分发与跟踪
* 统一外部事件接入
* 统一对外调用编排
* 统一审计和时间线追踪

### 2.2 核心业务流程

完整业务链路如下：

1. 医生申请单据 / 模拟定位申请
2. 模拟技师执行模拟定位并提交记录单
3. CT 图像入库与转发
4. PvMed 或第三方系统执行自动/人工勾画
5. 勾画审核
6. 计划待领与计划设计
7. 计划评估
8. 计划复审（可选）
9. 处方生成/同步
10. 计划核查 QA
11. 计划复核（可选）
12. MSQ 排程同步
13. 放疗医嘱提交
14. 排队叫号与治疗执行
15. 治疗完成判定
16. 放疗后评估
17. 病例归档

### 2.3 核心非功能需求

* 可靠性：必须支持 Outbox/Inbox 模式。
* 幂等性：外部事件必须支持幂等处理。
* 可追溯性：所有关键行为必须审计。
* 可配置性：支持 Hospital + Site + Department 三层配置。
* 可扩展性：支持新增外部系统、流程节点、表单类型。

---

## 3. 总体架构设计

### 3.1 架构原则

* **业务真相源在本地数据库**，外部引擎与系统不作为主状态真相源。
* **主干流程固定，插槽可配置**。
* **状态机负责病例生命周期**，任务负责执行动作。
* **系统集成通过事件与消息完成**，避免强耦合同步调用。
* **审计与历史先于便利性**，优先保证可追溯性。

### 3.2 分层架构

系统采用四层架构：

#### Wfmgr.Domain

* 枚举、常量、领域对象
* 状态与任务类型定义
* 核心领域约束抽象

#### Wfmgr.Application

* 工作流服务
* 状态机服务
* 表单服务
* 任务服务
* 应用 DTO
* 权限与校验协调逻辑

#### Wfmgr.Infrastructure

* EF Core DbContext
* 数据实体与配置
* PvMed/Monaco/MSQ 集成适配器
* Outbox Worker
* WorkflowProfileResolver
* 文件存储/附件存储适配

#### Wfmgr.Api

* Web API 控制器
* Swagger
* 身份认证接入
* 配置与依赖注入

### 3.3 核心逻辑分层

系统分为三条主线：

1. **Case 状态机**：控制病例从开始到归档的生命周期。
2. **WorkItem 任务流**：控制每个节点谁做、做什么、做完推进哪里。
3. **Integration 编排流**：控制与外部系统之间的消息往来、状态回传与重试。

---

## 4. 完整业务流程设计

### 4.1 Phase 1：申请与模拟定位

1. 医生创建病例，提交模拟定位申请单。
2. 模拟技师接收安排任务，预约模拟定位。
3. 模拟技师执行模拟定位，提交 CT 模拟记录单。
4. 系统等待 CT 影像入库事件。

### 4.2 Phase 2：图像处理与勾画

1. 系统接收 CT_IMAGE_STORED 事件。
2. 保存影像引用，病例进入影像已就绪状态。
3. 按配置决定是否自动转发至 PvMed 或第三方系统。
4. 跟踪自动勾画状态。
5. 勾画完成后创建审核任务。
6. 审核通过后进入计划阶段；审核驳回则进入修订阶段。

### 4.3 Phase 3：计划设计与评估

1. 病例进入计划待领。
2. 按配置自动分配或人工领取计划任务。
3. Monaco 完成计划设计。
4. 计划结果评估。
5. 可选复审。
6. 处方生成或同步。

### 4.4 Phase 4：QA 与复核

1. 处方和计划齐备后创建 QA 任务。
2. QA 通过则进入可排程状态。
3. 若配置启用双重复核，则再创建复核任务。
4. 不通过时回退到计划设计或复审阶段。

### 4.5 Phase 5：排程、医嘱与治疗

1. 等待 MSQ 排程同步。
2. 同步排程信息。
3. 医生提交放疗医嘱。
4. 系统进入队列待治疗。
5. 接收治疗启动和分次执行数据。
6. 根据完成判定策略确认疗程结束。

### 4.6 Phase 6：治疗后与归档

1. 创建放疗后评估任务。
2. 医生提交后评估表单。
3. 系统归档病例。

---

## 5. Case 状态机设计

### 5.1 状态列表

* Draft
* Submitted
* SimScheduled
* SimInProgress
* SimCompleted
* ImageStored
* ImageForwarding
* ContouringInProgress
* ContoursReady
* ContoursUnderReview
* ContoursRejected
* ContourReworkRequired
* PlanningPending
* PlanningAssigned
* PlanningInProgress
* PlanReady
* PlanUnderReview
* PlanReviewed
* PlanReReviewOptional
* PrescriptionGenerating
* PrescriptionReady
* PrescriptionSyncFailed
* PlanQAInProgress
* PlanQAApproved
* PlanQAFailed
* PlanDoubleCheckOptional
* ReadyForScheduling
* SchedulingInProgress
* Scheduled
* OrderPending
* OrderSubmitted
* QueuePending
* Treating
* TreatmentPaused
* TreatmentInterrupted
* TreatmentCompleted
* PostTreatmentReviewPending
* PostTreatmentReviewed
* Archived
* Cancelled

### 5.2 状态机原则

* 所有状态迁移必须通过统一状态机服务完成。
* 任何外部事件不能直接修改 Case 状态，必须通过状态机校验。
* 每次迁移必须写入：

  * Case.CurrentStatus
  * Case.StatusVersion
  * AuditLog
  * CaseTransitionHistory

### 5.3 主要门禁规则

* 模拟记录单已提交才能进入 SimCompleted。
* 影像引用存在才能进入 ImageStored。
* 勾画审核通过才能进入 PlanningPending。
* 计划版本存在才能进入 PlanReady / PlanUnderReview。
* 处方同步成功才能进入 QA。
* QA 通过才能进入 ReadyForScheduling。
* 治疗医嘱已提交才能进入治疗前队列。
* 后评估完成才能归档。

### 5.4 取消与中断规则

* Cancelled 仅允许在未进入治疗前的状态。
* 治疗开始后若中止，进入 TreatmentPaused 或 TreatmentInterrupted。

---

## 6. WorkItem 任务模型设计

### 6.1 设计原则

* 每个关键人工步骤或人工补救步骤必须对应 WorkItem。
* 任务先分配到角色，再可选分配到具体用户。
* 任务完成、驳回、取消、重试必须有统一动作。

### 6.2 WorkItem 类型

#### 模拟定位阶段

* SimulationRequest
* SimulationSchedule
* SimulationRecord

#### 图像与勾画阶段

* ImageValidation
* ImageForwardToContourTool
* AutoContourMonitor
* ManualContouring
* ContourReview
* ContourSecondReview
* ContourRework

#### 计划阶段

* PlanAssignment
* PlanDesign
* PlanEvaluation
* PlanReReview

#### 处方与 QA 阶段

* PrescriptionSync
* PlanQA
* PlanDoubleCheck

#### 排程与治疗阶段

* ScheduleSync
* TreatmentOrder
* QueueCall
* TreatmentMonitor
* TreatmentExceptionHandling

#### 治疗后阶段

* PostTreatmentReview
* ArchiveReview

### 6.3 WorkItem 扩展字段

建议字段包括：

* SequenceNo
* ParentWorkItemId
* WorkItemGroup
* ResultCode
* CompletedAt
* CompletedBy
* FormId
* RequiresDifferentUserFrom
* RetryCount
* Remarks

### 6.4 任务执行约束

* `RequiresDifferentUserFrom` 用于限制 QA 与复核不能同一人。
* 任务若因配置跳过，状态应为 `Skipped`。
* 被回退影响的任务应显式关闭或作废。

---

## 7. 配置模型设计

### 7.1 配置层级

WorkflowProfile 按三层解析：

1. Hospital + Site + Department
2. Hospital + Site
3. Hospital
4. Global Default

### 7.2 WorkflowProfile

作用：定义某一层级下的流程配置版本。
关键字段：

* ProfileId
* HospitalId
* SiteId
* DepartmentId
* Name
* Version
* IsActive
* CreatedAt

### 7.3 WorkflowRule

作用：定义具体插槽规则。
关键字段：

* RuleId
* ProfileId
* SlotCode
* Priority
* ConditionJson
* ConfigJson
* IsEnabled
* EffectiveFrom / EffectiveTo

### 7.4 关键配置插槽

* S1_CONTOURING_STRATEGY
* S2_CONTOUR_REVIEW_POLICY
* S3_PLAN_DISPATCH
* S4_PLAN_REREVIEW_POLICY
* S5_PLAN_DOUBLE_CHECK
* S6_QUEUE_AND_CANCEL_POLICY
* S7_TREATMENT_COMPLETION_POLICY
* S8_EXCEPTION_HANDLING_POLICY

### 7.5 S1 示例

```json
{
  "autoContourEnabled": true,
  "onAutoContourComplete": {
    "autoForwardToMonaco": true,
    "allowManualForward": true
  },
  "fallback": {
    "onFailureCreateManualWorkItem": true,
    "manualWorkItemRole": "Doctor"
  }
}
```

### 7.6 配置如何生效（执行语义）

在系统运行时，每次到达关键节点会按以下步骤解析并执行插槽配置：

1. 通过 `WorkflowProfileResolver` 按层级选中当前 Profile（Department -> Site -> Hospital -> Global）。
2. 在该 Profile 下按 `SlotCode` 找到启用的 `WorkflowRule`。
3. 若存在多条命中规则，按 `Priority` 从高到低取第一条；并校验 `EffectiveFrom/EffectiveTo`。
4. 将 `ConfigJson` 反序列化为对应插槽 DTO，执行字段级校验。
5. 由工作流服务在对应节点执行策略（创建 WorkItem、推进状态、发送 Outbox、记录审计）。

建议约束：

* 每个 Slot 在同一 Profile 同一条件下仅允许 1 条有效规则，避免歧义。
* 所有 Slot 必须有全局默认规则，避免因未配置阻塞主流程。
* `ConfigJson` 变更应版本化并写审计。

### 7.7 八个插槽配置模板（建议起步版）

以下模板是“可直接落库到 `WorkflowRule.ConfigJson`”的起步配置。

#### S1_CONTOURING_STRATEGY（勾画策略）

触发时机：病例进入图像就绪后，决定自动勾画、完成后推进与失败兜底。

```json
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
```

#### S2_CONTOUR_REVIEW_POLICY（勾画审核策略）

触发时机：`ContoursReady` 后创建审核任务，控制单审/复审与驳回回退。

```json
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
```

#### S3_PLAN_DISPATCH（计划派发策略）

触发时机：`PlanningPending`，决定自动分配还是人工领取。

```json
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
```

#### S4_PLAN_REREVIEW_POLICY（计划复审策略）

触发时机：`PlanReviewed` 后判断是否进入复审。

```json
{
  "enabled": true,
  "trigger": {
    "riskLevelIn": ["High"],
    "doseDeltaPercentGte": 5
  },
  "reviewRole": "SeniorPhysicist",
  "onRejectBackTo": "PlanningInProgress"
}
```

#### S5_PLAN_DOUBLE_CHECK（双重复核策略）

触发时机：QA 通过后，决定是否增加双重复核任务。

```json
{
  "enabled": true,
  "workItemRole": "QAReviewer",
  "requiresDifferentUserFrom": "PlanQA",
  "onFailBackTo": "PlanQAInProgress",
  "maxRetry": 1
}
```

#### S6_QUEUE_AND_CANCEL_POLICY（排队与取消策略）

触发时机：进入排队、叫号、取消时，控制可取消边界与取消后动作。

```json
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
```

#### S7_TREATMENT_COMPLETION_POLICY（治疗完成判定策略）

触发时机：接收治疗分次/疗程事件后，判断是否可进入 `TreatmentCompleted`。

```json
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
```

#### S8_EXCEPTION_HANDLING_POLICY（异常处理策略）

触发时机：外部调用失败、集成回调异常、状态推进失败等异常场景。

```json
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
```

### 7.8 与状态机/任务/集成的映射建议

* 状态机：插槽只决定“能否迁移、迁移到哪里、是否补充人工节点”，不直接绕过门禁规则。
* WorkItem：凡是人工介入、失败补救、超时升级都应显式创建/关闭 WorkItem，并写 `ResultCode`。
* Outbox/Inbox：插槽决定是否发送对外动作，但发送与回调仍必须经过 Outbox/Inbox 保障可靠性。
* 审计：每次命中规则时记录 `SlotCode`、`RuleId`、`ConfigJson` 摘要、执行结果。

### 7.9 默认值策略（建议）

* 默认优先“可继续推进但可审计”：尽量避免因未配置导致流程硬阻塞。
* 所有 `enabled` 字段缺省为 `false`，关键布尔项在 DTO 层显式赋默认。
* 枚举字段（如 `dispatchMode`）必须做白名单校验，非法值回退到全局默认规则。
* 任意配置解析失败时：写审计 + 进入异常插槽 S8 的处理路径。

### 7.10 插槽字段字典（S1-S8）

以下字段字典用于约束 `WorkflowRule.ConfigJson`，建议与后端 DTO 和校验器保持一致。

#### S1_CONTOURING_STRATEGY

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---|---|---|---|
| autoContourEnabled | bool | 是 | false | 是否启用自动勾画 |
| provider | string(enum) | 否 | PvMed | 自动勾画供应方，建议枚举：PvMed/ThirdParty |
| onAutoContourComplete | object | 否 | {} | 自动勾画完成后的推进策略 |
| onAutoContourComplete.autoForwardToMonaco | bool | 否 | false | 完成后是否自动推进到 Monaco |
| onAutoContourComplete.allowManualForward | bool | 否 | true | 是否允许人工手动推进 |
| fallback | object | 否 | {} | 失败兜底策略 |
| fallback.onFailureCreateManualWorkItem | bool | 否 | true | 自动勾画失败是否创建人工任务 |
| fallback.manualWorkItemType | string(enum) | 否 | ManualContouring | 人工兜底任务类型 |
| fallback.manualWorkItemRole | string(enum) | 否 | Doctor | 人工兜底任务角色 |

#### S2_CONTOUR_REVIEW_POLICY

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---|---|---|---|
| reviewMode | string(enum) | 是 | Single | 审核模式：Single/Double |
| allowSecondReview | bool | 否 | false | 是否允许二次审核 |
| onReject | object | 否 | {} | 驳回回退策略 |
| onReject.targetStatus | string(enum) | 否 | ContourReworkRequired | 驳回后的目标状态 |
| onReject.createReworkWorkItem | bool | 否 | true | 驳回后是否创建返工任务 |
| onReject.reworkWorkItemRole | string(enum) | 否 | Doctor | 返工任务角色 |
| timeoutHours | int | 否 | 24 | 审核超时小时数 |

#### S3_PLAN_DISPATCH

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---|---|---|---|
| dispatchMode | string(enum) | 是 | AutoAssignByRole | 派发模式：AutoAssignByRole/ManualClaimOnly |
| targetRole | string(enum) | 否 | Dosimetrist | 自动派发目标角色 |
| allowManualClaim | bool | 否 | true | 是否允许人工领取 |
| slaMinutes | int | 否 | 240 | 任务 SLA（分钟） |
| escalation | object | 否 | {} | 超时升级策略 |
| escalation.enabled | bool | 否 | false | 是否启用升级 |
| escalation.afterMinutes | int | 否 | 180 | 多久后触发升级 |
| escalation.escalateToRole | string(enum) | 否 | ChiefDoctor | 升级目标角色 |

#### S4_PLAN_REREVIEW_POLICY

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---|---|---|---|
| enabled | bool | 是 | false | 是否启用计划复审 |
| trigger | object | 否 | {} | 触发条件 |
| trigger.riskLevelIn | string[] | 否 | [] | 指定风险等级集合时触发 |
| trigger.doseDeltaPercentGte | number | 否 | null | 剂量偏差大于等于阈值时触发 |
| reviewRole | string(enum) | 否 | SeniorPhysicist | 复审角色 |
| onRejectBackTo | string(enum) | 否 | PlanningInProgress | 复审驳回后回退状态 |

#### S5_PLAN_DOUBLE_CHECK

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---|---|---|---|
| enabled | bool | 是 | false | 是否启用双重复核 |
| workItemRole | string(enum) | 否 | QAReviewer | 复核任务角色 |
| requiresDifferentUserFrom | string(enum) | 否 | PlanQA | 要求与哪个任务的处理人不同 |
| onFailBackTo | string(enum) | 否 | PlanQAInProgress | 复核失败后回退状态 |
| maxRetry | int | 否 | 1 | 最大重试次数 |

#### S6_QUEUE_AND_CANCEL_POLICY

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---|---|---|---|
| queueMode | string(enum) | 是 | MsqDriven | 排队模式：MsqDriven/ManualQueue |
| allowCancel | bool | 否 | true | 是否允许取消 |
| cancelAllowedBeforeStatus | string(enum) | 否 | Treating | 仅允许在该状态之前取消 |
| requireCancelReason | bool | 否 | true | 取消是否必须填写原因 |
| onCancel | object | 否 | {} | 取消后处理策略 |
| onCancel.closeOpenWorkItems | bool | 否 | true | 是否关闭未完成任务 |
| onCancel.createAudit | bool | 否 | true | 是否强制写取消审计 |
| onCancel.finalStatus | string(enum) | 否 | Cancelled | 取消后的最终状态 |

#### S7_TREATMENT_COMPLETION_POLICY

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---|---|---|---|
| mode | string(enum) | 是 | ByCourseCompletedEvent | 完成判定模式：ByFractions/ByCourseCompletedEvent |
| requiredFractions | int | 否 | null | 按分次判定时的总分次数 |
| acceptCourseCompletedEvent | bool | 否 | true | 是否接受疗程完成事件作为完成依据 |
| allowManualCompletion | bool | 否 | false | 是否允许人工强制完成 |
| onMismatch | object | 否 | {} | 判定不一致时处理策略 |
| onMismatch.createExceptionWorkItem | bool | 否 | true | 是否创建异常任务 |
| onMismatch.exceptionRole | string(enum) | 否 | Therapist | 异常任务角色 |

#### S8_EXCEPTION_HANDLING_POLICY

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|---|---|---|---|---|
| retry | object | 否 | {} | 重试策略 |
| retry.enabled | bool | 否 | true | 是否启用自动重试 |
| retry.maxAttempts | int | 否 | 5 | 最大重试次数 |
| retry.backoff | string(enum) | 否 | Exponential | 退避策略：Fixed/Exponential |
| retry.baseSeconds | int | 否 | 30 | 重试基准间隔（秒） |
| manualFallback | object | 否 | {} | 人工兜底策略 |
| manualFallback.enabled | bool | 否 | true | 是否启用人工兜底 |
| manualFallback.workItemType | string(enum) | 否 | TreatmentExceptionHandling | 异常任务类型 |
| manualFallback.workItemRole | string(enum) | 否 | Admin | 异常任务角色 |
| notify | object | 否 | {} | 通知策略 |
| notify.enabled | bool | 否 | false | 是否启用通知 |
| notify.channels | string[] | 否 | [] | 通知渠道：InApp/Email/SMS |

---

## 8. 表单体系设计

### 8.1 设计原则

* 表单与状态迁移解耦，但通过校验建立关联。
* 一种表单类型允许多个版本。
* 表单应保存原始 PayloadJson 以便审计和追溯。

### 8.2 表单类型

* SimulationRequestForm
* SimulationRecordForm
* ContourReviewForm
* PlanEvaluationForm
* PlanReReviewForm
* PlanQAForm
* PlanDoubleCheckForm
* TreatmentOrderForm
* PostTreatmentReviewForm
* CancellationForm

### 8.3 CaseForm 字段

* FormId
* CaseId
* FormType
* FormVersion
* Status
* PayloadJson
* SubmittedBy
* SubmittedAt
* CreatedAt
* UpdatedAt

### 8.4 表单与状态关系

* SimulationRequestForm 支持 Draft -> Submitted。
* SimulationRecordForm 支持 SimInProgress -> SimCompleted。
* ContourReviewForm 支持 ContoursUnderReview -> PlanningPending 或 ContoursRejected。
* PlanEvaluationForm 支持 PlanUnderReview 的评估通过/驳回。
* PlanQAForm 支持 QA 通过/失败。
* TreatmentOrderForm 支持 OrderPending -> OrderSubmitted。
* PostTreatmentReviewForm 支持 PostTreatmentReviewPending -> PostTreatmentReviewed。

---

## 9. 外部集成设计

### 9.1 集成原则

* 所有入站事件通过 ExternalEvent Inbox 接收。
* 所有出站调用通过 OutboxMessage 发出。
* 入站、出站均支持失败重试与幂等。

### 9.2 外部系统

* CT / 影像系统
* PvMed
* Monaco
* MSQ
* 第三方勾画系统

### 9.3 ExternalEvent 类型

#### CT / 影像系统

* CT_IMAGE_STORED
* CT_IMAGE_STORAGE_FAILED

#### PvMed / 勾画系统

* AUTOCONTOUR_STARTED
* AUTOCONTOUR_PROGRESS
* AUTOCONTOUR_COMPLETED
* AUTOCONTOUR_FAILED
* MANUAL_CONTOUR_COMPLETED

#### Monaco

* MONACO_IMPORT_ACCEPTED
* MONACO_IMPORT_FAILED
* PLAN_CREATED
* PLAN_UPDATED
* PLAN_REVIEW_COMPLETED
* PLAN_REVIEW_FAILED

#### MSQ

* PRESCRIPTION_GENERATED
* PRESCRIPTION_SYNC_FAILED
* SCHEDULE_SYNCED
* TREATMENT_STARTED
* TREATMENT_FRACTION_COMPLETED
* TREATMENT_COURSE_COMPLETED
* TREATMENT_INTERRUPTED

### 9.4 OutboxMessage 动作

* SendImagesToContourTool
* SendToMonacoImport
* QueryContourStatus
* GeneratePrescription
* SyncSchedule
* QueryTreatmentProgress

### 9.5 IntegrationReference

用于记录本地 Case 与外部系统实体之间的映射关系。
字段：

* Id
* CaseId
* SystemName
* ExternalEntityType
* ExternalId
* ExternalStatus
* MetadataJson
* CreatedAt
* UpdatedAt

---

## 10. 数据库详细设计

### 10.1 核心表

#### Case

* CaseId
* HospitalId
* SiteId
* DepartmentId
* PatientId
* AccessionNumber
* CurrentStatus
* StatusVersion
* CtStudyInstanceUid
* CtWadoRsUrl
* PvMedJobId
* RtStructSeriesInstanceUid
* Notes
* CurrentPlannerUserId
* CurrentReviewerUserId
* CurrentPlanVersionNo
* CreatedAt
* UpdatedAt

#### WorkItem

* WorkItemId
* CaseId
* Type
* Status
* AssignedRole
* AssignedUserId
* DueAt
* SlaMinutes
* ExternalCorrelationId
* PayloadJson
* SequenceNo
* ParentWorkItemId
* WorkItemGroup
* ResultCode
* CompletedAt
* CompletedBy
* FormId
* RequiresDifferentUserFrom
* RetryCount
* Remarks
* CreatedAt
* UpdatedAt

#### ExternalEvent

* EventId
* Source
* Type
* ExternalId
* CaseCorrelationKey
* CaseId
* PayloadJson
* ReceivedAt
* ProcessedAt
* ProcessStatus
* Error

唯一索引：

* (Source, Type, ExternalId)

#### OutboxMessage

* MessageId
* CaseId
* TargetSystem
* Action
* PayloadJson
* Status
* RetryCount
* NextRetryAt
* CreatedAt
* LastTriedAt

#### WorkflowProfile

* ProfileId
* HospitalId
* SiteId
* DepartmentId
* Name
* Version
* IsActive
* CreatedAt

#### WorkflowRule

* RuleId
* ProfileId
* SlotCode
* Priority
* ConditionJson
* ConfigJson
* IsEnabled
* EffectiveFrom
* EffectiveTo

#### AuditLog

* AuditId
* CaseId
* ActorType
* ActorId
* Action
* FromStatus
* ToStatus
* SnapshotJson
* CreatedAt

### 10.2 扩展表

#### CaseTransitionHistory

* TransitionId
* CaseId
* FromStatus
* ToStatus
* TriggerType
* TriggerName
* TriggeredBy
* Reason
* MetadataJson
* CreatedAt

#### CaseForm

* FormId
* CaseId
* FormType
* FormVersion
* Status
* PayloadJson
* SubmittedBy
* SubmittedAt
* CreatedAt
* UpdatedAt

#### CaseAttachment

* AttachmentId
* CaseId
* Category
* FileName
* StoragePath
* SourceSystem
* UploadedBy
* UploadedAt

#### IntegrationReference

* Id
* CaseId
* SystemName
* ExternalEntityType
* ExternalId
* ExternalStatus
* MetadataJson
* CreatedAt
* UpdatedAt

#### PlanVersion

* PlanVersionId
* CaseId
* VersionNo
* SourceSystem
* Status
* SummaryJson
* CreatedAt

---

## 11. API 设计

### 11.1 CasesController

* POST /api/cases
* GET /api/cases
* GET /api/cases/{caseId}
* POST /api/cases/{caseId}/sim-record
* POST /api/cases/{caseId}/forward/monaco
* GET /api/cases/{caseId}/work-items
* GET /api/cases/{caseId}/audit-logs
* GET /api/cases/{caseId}/transition-history
* GET /api/cases/{caseId}/forms
* GET /api/cases/{caseId}/attachments
* GET /api/cases/{caseId}/integration-references
* GET /api/cases/{caseId}/plan-versions

### 11.2 FormController

* POST /api/forms
* PUT /api/forms/{formId}
* POST /api/forms/{formId}/submit
* GET /api/forms/{formId}
* GET /api/cases/{caseId}/forms

### 11.3 Integration Controllers

#### CT

* POST /api/integration/ct/image-stored

#### PvMed

* POST /api/integration/pvmed/events

#### Monaco

* POST /api/integration/monaco/events

#### MSQ

* POST /api/integration/msq/events

### 11.4 Workflow Helper APIs

* GET /api/workflow/statuses
* GET /api/workflow/work-item-types

---

## 12. 前端测试控制台设计

### 12.1 目标

wfmgr-ui 用于测试和验证工作流，不是生产最终 UI。

### 12.2 页面结构

* Dashboard
* Case List
* Case Details
* Work Items
* Audit Timeline
* Transition History
* Forms
* External Events
* Integration References
* Plan Versions

### 12.3 Case Details 支持的动作

* 提交模拟记录
* 模拟 CT 影像入库事件
* 模拟勾画进度/完成/失败事件
* 审核/驳回勾画
* 分配计划人员
* 提交计划评估
* 提交 QA
* 提交放疗医嘱
* 模拟排程同步
* 模拟治疗开始/分次完成/疗程完成
* 提交治疗后评估

---

## 13. 审计与可追溯性设计

### 13.1 审计原则

以下动作必须写审计：

* 状态迁移
* 表单提交
* 对外发送
* 接收外部事件
* 审核通过/驳回
* 任务分配/完成/取消

### 13.2 审计内容

* 谁做的
* 什么时候做的
* 从什么状态到什么状态
* 使用了哪份表单或外部数据
* 快照内容

### 13.3 时间线展示

前端应按时间序展示：

* 状态迁移
* 任务变更
* 表单提交
* 外部事件
* Outbox 发送

---

## 14. 可靠性设计

### 14.1 Inbox 模式

所有外部回调先落库 ExternalEvent，再处理。
唯一索引保障幂等。

### 14.2 Outbox 模式

所有对外调用写入 OutboxMessage，再由后台 Worker 发送。

### 14.3 重试策略

* 指数退避
* 限制最大重试次数
* 超过阈值转人工任务

### 14.4 人工补救

以下场景需自动创建人工 WorkItem：

* 自动勾画失败
* Monaco 导入失败
* 处方同步失败
* 排程同步失败
* 治疗中断

---

## 15. 安全设计

### 15.1 认证与授权

建议接入统一身份认证系统。
后端按角色进行授权：

* Doctor
* ChiefDoctor
* SimTech
* Dosimetrist
* Physicist
* SeniorPhysicist
* Therapist
* QAReviewer
* Scheduler
* Admin
* System

### 15.2 数据安全

* 配置文件中不保存明文密钥。
* 数据库连接信息通过安全配置管理。
* 所有关键接口记录调用审计。

### 15.3 外部回调安全

建议支持：

* HMAC 签名
* 白名单
* 重放保护

---

## 16. 开发实施建议

### 16.1 分阶段落地顺序

1. 扩展完整状态集
2. 扩展数据库结构与 EF Core
3. 完善 WorkItem 体系
4. 实现状态机和门禁规则
5. 实现异常与回退流程
6. 实现表单体系
7. 扩展外部系统集成
8. 完善查询 API 与 Angular 测试控制台

### 16.2 代码组织建议

* 状态迁移逻辑集中到 `CaseStateMachine` 或 `CaseTransitionService`
* WorkItem 操作集中到 `WorkItemService`
* 表单操作集中到 `CaseFormService`
* 外部事件调度集中到 `ExternalEventDispatcher`
* 配置解析集中到 `WorkflowProfileResolver`

### 16.3 测试建议

* 单元测试：状态迁移与门禁规则
* 集成测试：ExternalEvent / Outbox 行为
* 端到端测试：Angular 测试控制台驱动完整流程

---

## 17. 风险与注意事项

### 17.1 医疗流程差异风险

不同医院的复审、复核、排程和叫号规则会不同，因此插槽配置必须优先设计好。

### 17.2 外部系统协议不一致风险

PvMed、Monaco、MSQ 的回调与数据模型可能差异大，建议通过标准化事件字典做隔离。

### 17.3 审计量大带来的性能风险

AuditLog、ExternalEvent、OutboxMessage 可能增长很快，应预留归档策略。

### 17.4 流程回退复杂度风险

异常流程一旦未设计清楚，后续极易形成状态脏数据，因此状态机服务必须强约束。

---

## 18. 结论

wfmgr 的详细设计应以 **病例状态机 + 任务执行层 + 外部集成编排层** 为核心，实现医疗放疗流程的完整闭环控制。

通过统一的状态迁移、任务模型、表单体系、配置插槽、审计追踪与 Inbox/Outbox 可靠性机制，系统可以满足：

* 固定主干流程的落地
* 少量可配置流程差异适配
* 多系统集成协同
* 医疗行业对可审计与可追溯的高要求

该设计适合继续驱动你当前的 `wfmgr` 项目向完整产品演进。
