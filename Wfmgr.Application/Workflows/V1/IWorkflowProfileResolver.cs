namespace Wfmgr.Application.Workflows.V1;

public interface IWorkflowProfileResolver
{
    Task<S1ContouringStrategy> ResolveS1ContouringStrategyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct);

    Task<S2ContourReviewPolicy> ResolveS2ContourReviewPolicyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct);

    Task<S3PlanDispatchPolicy> ResolveS3PlanDispatchPolicyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct);

    Task<S4PlanReReviewPolicy> ResolveS4PlanReReviewPolicyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct);

    Task<S5PlanDoubleCheckPolicy> ResolveS5PlanDoubleCheckPolicyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct);

    Task<S6CancelPolicy> ResolveS6CancelPolicyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct);

    Task<S7TreatmentCompletionPolicy> ResolveS7TreatmentCompletionPolicyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct);

    Task<S8ExceptionHandlingPolicy> ResolveS8ExceptionHandlingPolicyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct);
}
