namespace ProjectManagmentFlow.Services.Projects;

public interface IProjectQueryService
{
    Task<ProjectDetailRecord?> GetDetailAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// مشاريع الوحدة، أو الوحدة وما يتبعها  — وهو مفتاح «هذه الوحدة / وما يتبعها ».
    /// تُرجع الحالات كلّها بلا تصفية: أزرار التصفية تعرض عدّاداً لكلّ حالة،
    /// فتصفيتها هنا تُفقد الأعداد. التصفية بالحالة في المتحكّم.
    /// </summary>
    Task<List<ProjectCard>> GetByOrgAsync(
        Guid organizationId,
        bool includeDescendants,
        ProjectScope scope,
        string? search,
        CancellationToken cancellationToken = default);

    Task<ProjectStats> GetStatsAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<Dictionary<string, int>> GetTaskStatusCountsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<int> CountOverdueTasksAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>معاينة الرمز التالي؛ الرمز النهائي يُحسم عند الحفظ تحت قيد التفريد.</summary>
    Task<string> GetNextCodePreviewAsync(CancellationToken cancellationToken = default);

    /// <summary>الأعضاء النشطون في شجرة المنظّمة، من دون تكرار المستخدم.</summary>
    Task<List<ProjectPerson>> GetCandidatesAsync(
        Guid rootOrganizationId,
        CancellationToken cancellationToken = default);
}
