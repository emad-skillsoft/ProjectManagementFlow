using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Teams;

public interface ITeamQueryService
{
    Task<Team?> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<List<TeamMemberCard>> GetMembersAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);


    Task<int> CountMembersAsync(Guid projectId, CancellationToken cancellationToken = default);
}
