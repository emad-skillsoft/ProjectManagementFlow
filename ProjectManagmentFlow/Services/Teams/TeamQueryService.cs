using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Teams;

public sealed class TeamQueryService(AppDbContext context) : ITeamQueryService
{
    public Task<Team?> GetByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        context.Teams.AsNoTracking()
            .FirstOrDefaultAsync(team => team.ProjectId == projectId, cancellationToken);

    public async Task<List<TeamMemberCard>> GetMembersAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from team in context.Teams.AsNoTracking()
            join member in context.TeamMembers.AsNoTracking()
                on team.Id equals member.TeamId
            join user in context.Users.AsNoTracking()
                on member.UserId equals (Guid?)user.Id
            join project in context.Projects.AsNoTracking()
                on team.ProjectId equals (Guid?)project.Id
            where project.Id == projectId && project.DeletedAt == null
            orderby user.FullName
            select new
            {
                user.Id,
                Name = user.FullName ?? user.Email ?? "—",
                Email = user.Email ?? string.Empty,
                member.Role,
                IsProjectOwner = project.OwnerId == user.Id
            })
            .ToListAsync(cancellationToken);

        var userIds = rows.Select(row => row.Id).ToList();
        var openTasks = await context.Tasks.AsNoTracking()
            .Where(task => task.ProjectId == projectId
                           && task.AssigneeId != null
                           && userIds.Contains(task.AssigneeId.Value)
                           && task.Status != TaskState.Done
                           && task.Status != TaskState.Cancelled)
            .GroupBy(task => task.AssigneeId!.Value)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.UserId, row => row.Count, cancellationToken);

        var departments = await (
            from membership in context.OrgMembers.AsNoTracking()
            join organization in context.Organizations.AsNoTracking()
                on membership.OrganizationId equals (Guid?)organization.Id
            where membership.UserId != null
                  && userIds.Contains(membership.UserId.Value)
                  && membership.Status == OrgMemberStatus.Active
                  && organization.DeletedAt == null
            group organization by membership.UserId!.Value into memberships
            select new
            {
                UserId = memberships.Key,
                Name = memberships
                    .OrderByDescending(organization => organization.Depth)
                    .ThenBy(organization => organization.Path)
                    .Select(organization => organization.Name)
                    .First()
            })
            .ToDictionaryAsync(row => row.UserId, row => row.Name, cancellationToken);

        return rows.Select(row => new TeamMemberCard(
            row.Id,
            row.Name,
            row.Email,
            row.Role,
            departments.GetValueOrDefault(row.Id, "—"),
            openTasks.GetValueOrDefault(row.Id),
            row.IsProjectOwner)).ToList();
    }


    public Task<int> CountMembersAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        (from team in context.Teams.AsNoTracking()
         join member in context.TeamMembers.AsNoTracking() on team.Id equals member.TeamId
         where team.ProjectId == projectId
         select member.Id).CountAsync(cancellationToken);
}
