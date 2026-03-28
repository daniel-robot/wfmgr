namespace Wfmgr.Application.Workflows.V1;

public class S1ContouringStrategy
{
    public bool AutoContourEnabled { get; set; }
    public OnAutoContourCompleteConfig OnAutoContourComplete { get; set; } = new();
    public FallbackConfig Fallback { get; set; } = new();
}

public class OnAutoContourCompleteConfig
{
    public bool AutoForwardToMonaco { get; set; }
}

public class FallbackConfig
{
    public bool OnFailureCreateManualWorkItem { get; set; }
    public string ManualWorkItemRole { get; set; } = "Dosimetrist";
}
