using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Tasks;

public interface ITaskCommandService
{
    Task<ProjectTask> CreateAsync(
        Guid projectId,
        TaskCreateInput input,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>مهمّة بلا مشروع: تُسند إلى منشئها وتبقى خاصّةً به.</summary>
    Task<ProjectTask> CreatePersonalAsync(
        PersonalTaskInput input,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<ProjectTask> AddSubtaskAsync(
        Guid parentTaskId,
        string title,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Guid taskId,
        string title,
        string? description,
        DateOnly? dueDate,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task MoveAsync(
        Guid taskId,
        string status,
        Guid? afterTaskId,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task SetPriorityAsync(
        Guid taskId,
        string priority,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task AssignToAsync(
        Guid taskId,
        Guid? assigneeId,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task CancelAsync(Guid taskId, Guid actorId, CancellationToken cancellationToken = default);

    Task RestoreAsync(Guid taskId, Guid actorId, CancellationToken cancellationToken = default);
}
