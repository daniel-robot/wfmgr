namespace Wfmgr.Application.Workflows.V1;

public interface IWorkflowProfileResolver
{
    Task<S1ContouringStrategy> ResolveS1ContouringStrategyAsync(
        string hospitalId,
        string siteId,
        string departmentId,
        CancellationToken ct);
}
