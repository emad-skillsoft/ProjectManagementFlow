using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Organizations;

public sealed class OrgWorkspaceService(AppDbContext context) : IOrgWorkspaceService
{
    private const int RecentProjects = 3;
    private const int RecentActivity = 5;

    public async Task<OrgAccess> GetAccessAsync(
        Guid organizationId,
        Guid actorId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default)
    {
        var path = await context.Organizations.AsNoTracking()
            .Where(organization => organization.Id == organizationId && organization.DeletedAt == null)
            .Select(organization => organization.Path)
            .FirstOrDefaultAsync(cancellationToken);

        if (path is null) return OrgAccess.None;

        // دورٌ في المنظّمة أو في أيّ جدٍّ لها: مسار الجدّ بادئةٌ لمسارها.
        var roles = await (
            from member in context.OrgMembers.AsNoTracking()
            join organization in context.Organizations.AsNoTracking()
                on member.OrganizationId equals organization.Id
            where member.UserId == actorId
                  && member.Status == OrgMemberStatus.Active
                  && organization.DeletedAt == null
                  && path.StartsWith(organization.Path)
            select member.Role).ToListAsync(cancellationToken);

        return new OrgAccess(
            roles.Contains(OrgMemberRoles.Owner),
            roles.Contains(OrgMemberRoles.Admin),
            isPlatformAdmin);
    }

    public async Task<bool> ManagesAnyAsync(
        Guid actorId, bool isPlatformAdmin, CancellationToken cancellationToken = default) =>
        isPlatformAdmin
        || await context.OrgMembers.AsNoTracking().AnyAsync(member =>
            member.UserId == actorId
            && member.Status == OrgMemberStatus.Active
            && (member.Role == OrgMemberRoles.Owner || member.Role == OrgMemberRoles.Admin)
            && member.Organization!.DeletedAt == null, cancellationToken);

    public async Task<IReadOnlyList<OrgSwitchTarget>> GetSwitchTargetsAsync(
        Guid actorId,
        bool isPlatformAdmin,
        Guid currentId,
        CancellationToken cancellationToken = default)
    {
        // جذور الإدارة: ما هو مالكٌ أو نائبٌ فيه مباشرةً. وأدمن المنصّة جذورُه كلّ الشجرات.
        var roots = isPlatformAdmin
            ? await context.Organizations.AsNoTracking()
                .Where(organization => organization.DeletedAt == null && organization.ParentId == null)
                .Select(organization => organization.Path)
                .ToListAsync(cancellationToken)
            : await (
                from member in context.OrgMembers.AsNoTracking()
                join organization in context.Organizations.AsNoTracking()
                    on member.OrganizationId equals organization.Id
                where member.UserId == actorId
                      && member.Status == OrgMemberStatus.Active
                      && organization.DeletedAt == null
                      && (member.Role == OrgMemberRoles.Owner || member.Role == OrgMemberRoles.Admin)
                select organization.Path).ToListAsync(cancellationToken);

        if (roots.Count == 0) return [];

        var outermost = OrgScope.Outermost(roots);
        var rows = await context.Organizations.AsNoTracking()
            .Where(organization => organization.DeletedAt == null)
            .Select(organization => new
            {
                organization.Id,
                organization.Name,
                organization.Path,
                organization.Depth,
                organization.ParentId,
                Projects = organization.Projects.Count(project => project.DeletedAt == null),
                Members = organization.Members.Count(member => member.Status == OrgMemberStatus.Active)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Where(row => OrgScope.Contains(outermost, row.Path))
            .OrderBy(row => row.Path, StringComparer.Ordinal)
            .Select(row => new OrgSwitchTarget(
                row.Id, row.Name, row.Depth, row.ParentId is null,
                row.Projects, row.Members, row.Id == currentId))
            .ToList();
    }

    public async Task<OrgDashboard> GetDashboardAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var scope = await ScopeAsync(organizationId, cancellationToken);
        if (scope is null) return new OrgDashboard(0, 0, 0, 0, [], []);

        var projects = context.Projects.AsNoTracking()
            .Where(project => project.DeletedAt == null
                              && project.Organization!.Path.StartsWith(scope));

        var today = DateOnly.FromDateTime(DisplayTime.RiyadhNow());
        var tasks = context.Tasks.AsNoTracking()
            .Where(task => task.ProjectId != null
                           && task.Project!.DeletedAt == null
                           && task.Project.Organization!.Path.StartsWith(scope));

        var recent = await projects
            .OrderByDescending(project => project.CreatedAt)
            .Take(RecentProjects)
            .Select(project => new OrgProjectProgress(
                project.Id,
                project.Code,
                project.Name,
                project.Tasks.Count(task => task.ParentTaskId == null && task.CompletedAt != null),
                project.Tasks.Count(task => task.ParentTaskId == null)))
            .ToListAsync(cancellationToken);

        var activity = await (
            from entry in context.ActivityLog.AsNoTracking()
            join organization in context.Organizations.AsNoTracking()
                on entry.OrganizationId equals organization.Id
            join actor in context.Users.AsNoTracking()
                on entry.ActorId equals (Guid?)actor.Id into actors
            from actor in actors.DefaultIfEmpty()
            where organization.Path.StartsWith(scope)
            orderby entry.CreatedAt descending, entry.Id descending
            select new OrgActivityEntry(
                entry.EntityType,
                entry.Action,
                entry.Payload,
                actor == null ? null : actor.FullName ?? actor.Email,
                entry.CreatedAt))
            .Take(RecentActivity)
            .ToListAsync(cancellationToken);

        return new OrgDashboard(
            await projects.CountAsync(project => project.Status == ProjectStatus.Active, cancellationToken),
            await context.OrgMembers.AsNoTracking().CountAsync(member =>
                member.Status == OrgMemberStatus.Active
                && member.Organization!.Path.StartsWith(scope), cancellationToken),
            await tasks.CountAsync(task =>
                task.Status != TaskState.Done && task.Status != TaskState.Cancelled, cancellationToken),
            await tasks.CountAsync(task =>
                task.DueDate != null && task.DueDate < today
                && task.Status != TaskState.Done && task.Status != TaskState.Cancelled, cancellationToken),
            recent,
            activity);
    }

    public async Task<IReadOnlyList<OrgMemberCard>> GetMemberCardsAsync(
        Guid organizationId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var scope = await ScopeAsync(organizationId, cancellationToken);
        if (scope is null) return [];

        var rows = await (
            from member in context.OrgMembers.AsNoTracking()
            join organization in context.Organizations.AsNoTracking()
                on member.OrganizationId equals organization.Id
            join user in context.Users.AsNoTracking() on member.UserId equals user.Id
            where organization.DeletedAt == null && organization.Path.StartsWith(scope)
            orderby organization.Path, member.Role, user.FullName
            select new OrgMemberCard(
                user.Id,
                user.FullName ?? user.Email ?? string.Empty,
                user.Email ?? string.Empty,
                member.Role!,
                member.Status!,
                organization.Id,
                organization.Name,
                user.Id == actorId)).ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<IReadOnlyList<OrgInviteCandidate>> GetInviteCandidatesAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default) =>
        await (
            from user in context.Users.AsNoTracking()
            where user.IsActive
                  && !context.OrgMembers.Any(member =>
                      member.OrganizationId == organizationId && member.UserId == user.Id)
            orderby user.FullName
            select new OrgInviteCandidate(
                user.Id,
                user.FullName ?? user.Email ?? string.Empty,
                user.Email ?? string.Empty)).ToListAsync(cancellationToken);

    private Task<string?> ScopeAsync(Guid organizationId, CancellationToken cancellationToken) =>
        context.Organizations.AsNoTracking()
            .Where(organization => organization.Id == organizationId && organization.DeletedAt == null)
            .Select(organization => organization.Path)
            .FirstOrDefaultAsync(cancellationToken);
}
