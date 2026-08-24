using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Projects;

public interface IProjectCommandService
{
    /// <summary>
    /// يفحص مسوّدة الإنشاء بحرّاس الأمر نفسها بلا حفظ — تستعملها الخطوة الأولى
    /// من المعالج كي لا يُعاد كتابة الفحص في المتحكّم فيتباعد الاثنان.
    /// يرمي DomainException عند أوّل مخالفة.
    /// </summary>
    Task ValidateDraftAsync(
        Guid organizationId,
        string name,
        string? description,
        string status,
        string priority,
        DateOnly? startDate,
        DateOnly? dueDate,
        Guid? ownerId,
        CancellationToken cancellationToken = default);

    Task<Project> CreateAsync(
        Guid organizationId,
        string name,
        string? description,
        string priority,
        DateOnly? startDate,
        DateOnly? dueDate,
        Guid? ownerId,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ينشئ المشروع وفريقه وعضوياته في معاملة واحدة، بعد التحقق من نطاق المنشئ.
    /// </summary>
    Task<ProjectProvisionResult> CreateWithTeamAsync(
        Guid organizationId,
        string name,
        string? description,
        string status,
        string priority,
        DateOnly? startDate,
        DateOnly? dueDate,
        Guid ownerId,
        string teamName,
        IReadOnlyCollection<ProjectTeamMember> members,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Guid projectId,
        string name,
        string? description,
        DateOnly? startDate,
        DateOnly? dueDate,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task SetStatusAsync(
        Guid projectId, string status, Guid actorId,
        CancellationToken cancellationToken = default);
    Task SetOwnerAsync(
        Guid projectId, Guid ownerId, Guid actorId,
        CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid projectId, Guid actorId, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid projectId, Guid actorId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid projectId, Guid actorId, CancellationToken cancellationToken = default);

    Task MoveToOrgAsync(
        Guid projectId, Guid newOrganizationId, Guid actorId,
        CancellationToken cancellationToken = default);
}
