using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Tasks;

public interface ITaskQueryService
{
    Task<TaskBoard> GetBoardAsync(
        Guid projectId,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<TaskDetail?> GetDetailAsync(Guid taskId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskCard>> GetMyTasksAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
