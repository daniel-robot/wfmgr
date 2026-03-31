namespace Wfmgr.Application.Workflows.V1;

using Wfmgr.Domain.Enums;

public static class WorkflowSlotConfigValidator
{
    public static IReadOnlyList<string> Validate(string slotCode, object config)
    {
        return slotCode switch
        {
            WorkflowSlotCodes.S1ContouringStrategy when config is S1ContouringStrategy s1 => ValidateS1(s1),
            WorkflowSlotCodes.S2ContourReviewPolicy when config is S2ContourReviewPolicy s2 => ValidateS2(s2),
            WorkflowSlotCodes.S3PlanDispatch when config is S3PlanDispatchPolicy s3 => ValidateS3(s3),
            WorkflowSlotCodes.S4PlanReReviewPolicy when config is S4PlanReReviewPolicy s4 => ValidateS4(s4),
            WorkflowSlotCodes.S5PlanDoubleCheck when config is S5PlanDoubleCheckPolicy s5 => ValidateS5(s5),
            WorkflowSlotCodes.S6QueueAndCancelPolicy when config is S6QueueAndCancelPolicy s6 => ValidateS6(s6),
            WorkflowSlotCodes.S7TreatmentCompletionPolicy when config is S7TreatmentCompletionPolicy s7 => ValidateS7(s7),
            WorkflowSlotCodes.S8ExceptionHandlingPolicy when config is S8ExceptionHandlingPolicy s8 => ValidateS8(s8),
            _ => ["Unsupported slot code or config type."]
        };
    }

    private static IReadOnlyList<string> ValidateS1(S1ContouringStrategy config)
    {
        var errors = new List<string>();
        ValidateOneOf(config.Provider, ["PvMed", "ThirdParty"], "provider", errors);
        ValidateNotEmpty(config.Fallback.ManualWorkItemRole, "fallback.manualWorkItemRole", errors);
        ValidateNotEmpty(config.Fallback.ManualWorkItemType, "fallback.manualWorkItemType", errors);
        return errors;
    }

    private static IReadOnlyList<string> ValidateS2(S2ContourReviewPolicy config)
    {
        var errors = new List<string>();
        ValidateOneOf(config.ReviewMode, ["Single", "Double"], "reviewMode", errors);
        ValidateNotEmpty(config.OnReject.TargetStatus, "onReject.targetStatus", errors);
        ValidateNotEmpty(config.OnReject.ReworkWorkItemRole, "onReject.reworkWorkItemRole", errors);
        if (config.TimeoutHours <= 0)
        {
            errors.Add("timeoutHours must be greater than 0.");
        }

        return errors;
    }

    private static IReadOnlyList<string> ValidateS3(S3PlanDispatchPolicy config)
    {
        var errors = new List<string>();
        ValidateOneOf(config.DispatchMode, ["AutoAssignByRole", "ManualClaimOnly"], "dispatchMode", errors);
        ValidateNotEmpty(config.TargetRole, "targetRole", errors);
        if (config.SlaMinutes <= 0)
        {
            errors.Add("slaMinutes must be greater than 0.");
        }

        if (config.Escalation.Enabled && config.Escalation.AfterMinutes <= 0)
        {
            errors.Add("escalation.afterMinutes must be greater than 0 when escalation is enabled.");
        }

        return errors;
    }

    private static IReadOnlyList<string> ValidateS4(S4PlanReReviewPolicy config)
    {
        var errors = new List<string>();
        ValidateNotEmpty(config.ReviewRole, "reviewRole", errors);
        ValidateOneOf(config.OnRejectBackTo, ["PlanningInProgress"], "onRejectBackTo", errors);

        if (config.Trigger.DoseDeltaPercentGte is < 0)
        {
            errors.Add("trigger.doseDeltaPercentGte cannot be negative.");
        }

        return errors;
    }

    private static IReadOnlyList<string> ValidateS5(S5PlanDoubleCheckPolicy config)
    {
        var errors = new List<string>();
        ValidateNotEmpty(config.WorkItemRole, "workItemRole", errors);
        ValidateNotEmpty(config.RequiresDifferentUserFrom, "requiresDifferentUserFrom", errors);
        ValidateNotEmpty(config.OnFailBackTo, "onFailBackTo", errors);
        if (config.MaxRetry < 0)
        {
            errors.Add("maxRetry cannot be negative.");
        }

        return errors;
    }

    private static IReadOnlyList<string> ValidateS6(S6QueueAndCancelPolicy config)
    {
        var errors = new List<string>();
        ValidateOneOf(config.QueueMode, ["MsqDriven", "ManualQueue"], "queueMode", errors);
        ValidateNotEmpty(config.CancelAllowedBeforeStatus, "cancelAllowedBeforeStatus", errors);
        if (!Enum.TryParse<CaseStatus>(config.CancelAllowedBeforeStatus, ignoreCase: true, out _))
        {
            errors.Add("cancelAllowedBeforeStatus must be a valid CaseStatus value.");
        }

        ValidateNotEmpty(config.OnCancel.FinalStatus, "onCancel.finalStatus", errors);
        return errors;
    }

    private static IReadOnlyList<string> ValidateS7(S7TreatmentCompletionPolicy config)
    {
        var errors = new List<string>();
        ValidateOneOf(config.Mode, ["ByFractions", "ByCourseCompletedEvent"], "mode", errors);
        if (config.Mode == "ByFractions" && (config.RequiredFractions is null || config.RequiredFractions <= 0))
        {
            errors.Add("requiredFractions must be greater than 0 when mode is ByFractions.");
        }

        ValidateNotEmpty(config.OnMismatch.ExceptionRole, "onMismatch.exceptionRole", errors);
        return errors;
    }

    private static IReadOnlyList<string> ValidateS8(S8ExceptionHandlingPolicy config)
    {
        var errors = new List<string>();
        if (config.Retry.MaxAttempts <= 0)
        {
            errors.Add("retry.maxAttempts must be greater than 0.");
        }

        if (config.Retry.BaseSeconds <= 0)
        {
            errors.Add("retry.baseSeconds must be greater than 0.");
        }

        ValidateOneOf(config.Retry.Backoff, ["Fixed", "Exponential"], "retry.backoff", errors);
        ValidateNotEmpty(config.ManualFallback.WorkItemType, "manualFallback.workItemType", errors);
        ValidateNotEmpty(config.ManualFallback.WorkItemRole, "manualFallback.workItemRole", errors);

        var validChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InApp", "Email", "SMS" };
        var invalidChannels = config.Notify.Channels.Where(x => !validChannels.Contains(x)).ToArray();
        if (invalidChannels.Length > 0)
        {
            errors.Add($"notify.channels contains unsupported values: {string.Join(", ", invalidChannels)}.");
        }

        return errors;
    }

    private static void ValidateOneOf(string? value, IEnumerable<string> allowed, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
            return;
        }

        if (!allowed.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"{fieldName} must be one of: {string.Join(", ", allowed)}.");
        }
    }

    private static void ValidateNotEmpty(string? value, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
        }
    }
}
