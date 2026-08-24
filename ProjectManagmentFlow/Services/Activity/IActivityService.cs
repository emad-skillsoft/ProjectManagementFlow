namespace ProjectManagmentFlow.Services.Activity;

public sealed record ProjectActivityRecord(
    Guid? ActorId,
    string? ActorName,
    string EntityType,
    string Action,
    string? Payload,
    DateTime CreatedAt);

public interface IActivityService
{
    /// <summary>
    /// يضيف السجلّ إلى سياق EF بلا حفظ؛ يحفظه أمر المجال مع تغييره في عملية واحدة.
    /// </summary>
    void Add(
        Guid? organizationId,
        Guid? projectId,
        Guid? actorId,
        string entityType,
        Guid entityId,
        string action,
        object? payload = null);

    Task<List<ProjectActivityRecord>> ForProjectAsync(
        Guid projectId,
        int take = 50,
        CancellationToken cancellationToken = default);
}
