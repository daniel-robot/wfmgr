using Wfmgr.Domain;

namespace Wfmgr.Application.Workflows.V1;

public class S1ContouringStrategy
{
    public bool AutoContourEnabled { get; set; }
    public string Provider { get; set; } = "PvMed";
    public OnAutoContourCompleteConfig OnAutoContourComplete { get; set; } = new();
    public FallbackConfig Fallback { get; set; } = new();
}

public class OnAutoContourCompleteConfig
{
    public bool AutoForwardToMonaco { get; set; }
    public bool AllowManualForward { get; set; } = true;
}

public class FallbackConfig
{
    public bool OnFailureCreateManualWorkItem { get; set; } = true;
    public string ManualWorkItemType { get; set; } = "ManualContouring";
    public string ManualWorkItemRole { get; set; } = WorkflowRoles.Doctor;
}

public class S2ContourReviewPolicy
{
    public string ReviewMode { get; set; } = "Single";
    public bool AllowSecondReview { get; set; }
    public S2OnRejectConfig OnReject { get; set; } = new();
    public int TimeoutHours { get; set; } = 24;
}

public class S2OnRejectConfig
{
    public string TargetStatus { get; set; } = "ContourReworkRequired";
    public bool CreateReworkWorkItem { get; set; } = true;
    public string ReworkWorkItemRole { get; set; } = WorkflowRoles.Doctor;
}

public class S3PlanDispatchPolicy
{
    public string DispatchMode { get; set; } = "AutoAssignByRole";
    public string TargetRole { get; set; } = WorkflowRoles.Dosimetrist;
    public bool AllowManualClaim { get; set; } = true;
    public int SlaMinutes { get; set; } = 240;
    public S3EscalationConfig Escalation { get; set; } = new();
}

public class S3EscalationConfig
{
    public bool Enabled { get; set; }
    public int AfterMinutes { get; set; } = 180;
    public string EscalateToRole { get; set; } = WorkflowRoles.ChiefDoctor;
}

public class S4PlanReReviewPolicy
{
    public bool Enabled { get; set; }
    public S4TriggerConfig Trigger { get; set; } = new();
    public string ReviewRole { get; set; } = WorkflowRoles.SeniorPhysicist;
    public string OnRejectBackTo { get; set; } = "PlanningInProgress";
}

public class S4TriggerConfig
{
    public string[] RiskLevelIn { get; set; } = [];
    public decimal? DoseDeltaPercentGte { get; set; }
}

public class S5PlanDoubleCheckPolicy
{
    public bool Enabled { get; set; }
    public string WorkItemRole { get; set; } = "QAReviewer";
    public string RequiresDifferentUserFrom { get; set; } = "PlanQA";
    public string OnFailBackTo { get; set; } = "PlanQAInProgress";
    public int MaxRetry { get; set; } = 1;
}

public class S6QueueAndCancelPolicy
{
    public string QueueMode { get; set; } = "MsqDriven";
    public bool AllowCancel { get; set; } = true;
    public string CancelAllowedBeforeStatus { get; set; } = "Treating";
    public bool RequireCancelReason { get; set; } = true;
    public S6OnCancelConfig OnCancel { get; set; } = new();
}

public class S6OnCancelConfig
{
    public bool CloseOpenWorkItems { get; set; } = true;
    public bool CreateAudit { get; set; } = true;
    public string FinalStatus { get; set; } = "Cancelled";
}

public class S7TreatmentCompletionPolicy
{
    public string Mode { get; set; } = "ByCourseCompletedEvent";
    public int? RequiredFractions { get; set; }
    public bool AcceptCourseCompletedEvent { get; set; } = true;
    public bool AllowManualCompletion { get; set; }
    public S7OnMismatchConfig OnMismatch { get; set; } = new();
}

public class S7OnMismatchConfig
{
    public bool CreateExceptionWorkItem { get; set; } = true;
    public string ExceptionRole { get; set; } = "Therapist";
}

public class S8ExceptionHandlingPolicy
{
    public S8RetryConfig Retry { get; set; } = new();
    public S8ManualFallbackConfig ManualFallback { get; set; } = new();
    public S8NotifyConfig Notify { get; set; } = new();
}

public class S8RetryConfig
{
    public bool Enabled { get; set; } = true;
    public int MaxAttempts { get; set; } = 5;
    public string Backoff { get; set; } = "Exponential";
    public int BaseSeconds { get; set; } = 30;
}

public class S8ManualFallbackConfig
{
    public bool Enabled { get; set; } = true;
    public string WorkItemType { get; set; } = "TreatmentExceptionHandling";
    public string WorkItemRole { get; set; } = "Admin";
}

public class S8NotifyConfig
{
    public bool Enabled { get; set; }
    public string[] Channels { get; set; } = [];
}
