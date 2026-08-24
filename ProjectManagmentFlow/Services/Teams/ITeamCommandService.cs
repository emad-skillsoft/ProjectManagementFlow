using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Teams;

public interface ITeamCommandService
{
    /// <summary>فريق المشروع، ويُنشأ باسمه إن لم يكن موجوداً.</summary>
    Task<Team> EnsureForProjectAsync(
        Guid projectId,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task AddMemberAsync(
        Guid projectId,
        Guid userId,
        string role,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(
        Guid projectId,
        Guid userId,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task SetRoleAsync(
        Guid projectId,
        Guid userId,
        string role,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
