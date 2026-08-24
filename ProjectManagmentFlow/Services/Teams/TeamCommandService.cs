using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services;
using ProjectManagmentFlow.Services.Activity;
using ProjectManagmentFlow.Services.Projects;

namespace ProjectManagmentFlow.Services.Teams;

public sealed class TeamCommandService(
    AppDbContext context,
    IActivityService activity,
    IProjectQueryService projects,
    IStringLocalizer<Messages> text) : ITeamCommandService
{
    public async Task<Team> EnsureForProjectAsync(
        Guid projectId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var project = await LiveProjectAsync(projectId, cancellationToken)
            ?? throw new DomainException(text["Project_NotFound"]);

        var team = await GetOrCreateTeamAsync(project, actorId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return team;
    }

    public async Task AddMemberAsync(
        Guid projectId,
        Guid userId,
        string role,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        RequireKnownRole(role);
        var project = await LiveProjectAsync(projectId, cancellationToken)
            ?? throw new DomainException(text["Project_NotFound"]);
        await RequireCandidateAsync(project, userId, cancellationToken);

        var team = await GetOrCreateTeamAsync(project, actorId, cancellationToken);
        if (await context.TeamMembers.AnyAsync(
                member => member.TeamId == team.Id && member.UserId == userId,
                cancellationToken))
        {
            throw new DomainException(text["Team_MemberExists"], "userId");
        }

        await DemoteCurrentAsync(team.Id, role, null, cancellationToken);
        var member = new TeamMember
        {
            TeamId = team.Id,
            UserId = userId,
            Role = role,
            AddedById = actorId,
            JoinedAt = DateTime.UtcNow
        };
        context.TeamMembers.Add(member);
        team.UpdatedAt = DateTime.UtcNow;

        var name = await UserNameAsync(userId, cancellationToken);
        activity.Add(
            project.OrganizationId,
            project.Id,
            actorId,
            ActivityEntities.Member,
            member.Id,
            ActivityActions.Added,
            new { Name = name, Value = role });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMemberAsync(
        Guid projectId,
        Guid userId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var project = await LiveProjectAsync(projectId, cancellationToken)
            ?? throw new DomainException(text["Project_NotFound"]);
        var member = await MemberAsync(projectId, userId, cancellationToken)
            ?? throw new DomainException(text["Team_MemberNotFound"], "userId");
        if (member.Role == TeamMemberRoles.Leader)
        {
            throw new DomainException(text["Team_LastLead"], "role");
        }

        var name = await UserNameAsync(userId, cancellationToken);
        context.TeamMembers.Remove(member);
        activity.Add(
            project.OrganizationId,
            project.Id,
            actorId,
            ActivityEntities.Member,
            member.Id,
            ActivityActions.Removed,
            new { Name = name, Value = member.Role });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetRoleAsync(
        Guid projectId,
        Guid userId,
        string role,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        RequireKnownRole(role);
        var project = await LiveProjectAsync(projectId, cancellationToken)
            ?? throw new DomainException(text["Project_NotFound"]);
        var member = await MemberAsync(projectId, userId, cancellationToken)
            ?? throw new DomainException(text["Team_MemberNotFound"], "userId");
        if (member.Role == role) return;
        if (member.Role == TeamMemberRoles.Leader && role != TeamMemberRoles.Leader)
        {
            throw new DomainException(text["Team_LastLead"], "role");
        }

        await DemoteCurrentAsync(member.TeamId!.Value, role, member.Id, cancellationToken);
        member.Role = role;
        activity.Add(
            project.OrganizationId,
            project.Id,
            actorId,
            ActivityEntities.Member,
            member.Id,
            ActivityActions.Assigned,
            new { Name = await UserNameAsync(userId, cancellationToken), Value = role });
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Team> GetOrCreateTeamAsync(
        Project project,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var team = await context.Teams
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == project.Id, cancellationToken);
        if (team is not null) return team;

        team = Team.ForProject(project, null, actorId);
        context.Teams.Add(team);
        activity.Add(
            project.OrganizationId,
            project.Id,
            actorId,
            ActivityEntities.Team,
            team.Id,
            ActivityActions.Created,
            new { team.Name });
        return team;
    }

    private async Task DemoteCurrentAsync(
        Guid teamId,
        string targetRole,
        Guid? exceptMemberId,
        CancellationToken cancellationToken)
    {
        if (targetRole is not (TeamMemberRoles.Leader or TeamMemberRoles.Deputy)) return;

        var current = await context.TeamMembers
            .Where(member => member.TeamId == teamId
                             && member.Role == targetRole
                             && (exceptMemberId == null || member.Id != exceptMemberId))
            .ToListAsync(cancellationToken);
        foreach (var member in current) member.Role = TeamMemberRoles.Member;
    }

    /// <summary>قاعدة «من يصلح عضواً» يملكها IProjectQueryService وحده، فلا تُكتب هنا ثانيةً.</summary>
    private async Task RequireCandidateAsync(
        Project project,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rootId = await context.Organizations.AsNoTracking()
            .Where(organization => organization.Id == project.OrganizationId
                                   && organization.DeletedAt == null)
            .Select(organization => organization.RootId)
            .FirstOrDefaultAsync(cancellationToken);

        var candidates = rootId == Guid.Empty
            ? []
            : await projects.GetCandidatesAsync(rootId, cancellationToken);

        if (candidates.All(candidate => candidate.Id != userId))
        {
            throw new DomainException(text["Team_MemberNotFound"], "userId");
        }
    }

    private Task<Project?> LiveProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        context.Projects.FirstOrDefaultAsync(
            project => project.Id == projectId && project.DeletedAt == null,
            cancellationToken);

    private Task<TeamMember?> MemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken) =>
        (from team in context.Teams
         join member in context.TeamMembers on team.Id equals member.TeamId
         where team.ProjectId == projectId && member.UserId == userId
         select member).FirstOrDefaultAsync(cancellationToken);

    private async Task<string> UserNameAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.FullName ?? user.Email ?? "—")
            .FirstOrDefaultAsync(cancellationToken) ?? "—";

    private void RequireKnownRole(string role)
    {
        if (!TeamMemberRoles.IsKnown(role))
        {
            throw new DomainException(text["Team_UnknownRole"], "role");
        }
    }
}
