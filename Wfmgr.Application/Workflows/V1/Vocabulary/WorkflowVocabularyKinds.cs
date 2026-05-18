namespace Wfmgr.Application.Workflows.V1.Vocabulary;

/// <summary>
/// Discriminator codes for <c>WorkflowVocabularyTermEntity.Kind</c>.
/// Each kind seeds from a corresponding in-code constants class:
/// <list type="bullet">
///   <item><see cref="Role"/> ← <c>Wfmgr.Domain.WorkflowRoles</c></item>
///   <item><see cref="WorkItemType"/> ← <c>Wfmgr.Domain.WorkItems.WorkItemTypes</c></item>
///   <item><see cref="CaseFormType"/> ← <c>Wfmgr.Domain.Forms.CaseFormTypes</c></item>
/// </list>
/// </summary>
public static class WorkflowVocabularyKinds
{
    public const string Role = nameof(Role);
    public const string WorkItemType = nameof(WorkItemType);
    public const string CaseFormType = nameof(CaseFormType);

    public static readonly IReadOnlyList<string> All = [Role, WorkItemType, CaseFormType];

    public static bool IsValid(string? kind) =>
        !string.IsNullOrWhiteSpace(kind) && All.Contains(kind, StringComparer.Ordinal);
}
