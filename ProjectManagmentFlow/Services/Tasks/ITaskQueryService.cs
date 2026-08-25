using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Tasks;

public interface ITaskQueryService
{
    Task<TaskBoard> GetBoardAsync(
        Guid projectId,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<TaskDetail?> GetDetailAsync(Guid taskId, CancellationToken cancellationToken = default);

    Task<MyTasksView> GetMyTasksAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TeamTaskTarget>> GetTeamTaskTargetsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
