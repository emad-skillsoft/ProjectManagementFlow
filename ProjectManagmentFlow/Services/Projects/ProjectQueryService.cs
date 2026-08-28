using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services.Organizations;

namespace ProjectManagmentFlow.Services.Projects;

/// <summary>
/// قراءات المشاريع لشاشة القائمة. الاستعلامات هنا لا تعرف العرض:
/// تُرجع سجلّات خاماً ويحوّلها المتحكّم إلى ViewModels.
/// </summary>
public class ProjectQueryService : IProjectQueryService
{
    private readonly AppDbContext _context;
    private readonly IOrganizationQueryService _organizations;
    private readonly IStringLocalizer<Messages> _text;

    // طلب الإنشاء الواحد يسأل عن المرشّحين ثلاث مرّات (فحص · شاشة · فريق).
    // الخدمة Scoped، فالحفظ هنا يعيش عمر الطلب لا أكثر.
    private readonly Dictionary<Guid, List<ProjectPerson>> _candidateCache = [];

    public ProjectQueryService(
        AppDbContext context,
        IOrganizationQueryService organizations,
        IStringLocalizer<Messages> text)
    {
        _context = context;
        _organizations = organizations;
        _text = text;
    }

    public Task<ProjectDetailRecord?> GetDetailAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        (from project in Live()
         join organization in _context.Organizations.AsNoTracking()
             on project.OrganizationId equals (Guid?)organization.Id
         join ownerUser in _context.Users.AsNoTracking()
             on project.OwnerId equals (Guid?)ownerUser.Id into ownerUsers
         from owner in ownerUsers.DefaultIfEmpty()
         join creatorUser in _context.Users.AsNoTracking()
             on project.CreatedById equals (Guid?)creatorUser.Id into creatorUsers
         from creator in creatorUsers.DefaultIfEmpty()
         where project.Id == projectId && organization.DeletedAt == null
         select new ProjectDetailRecord(
             project.Id,
             project.Code,
             project.Name,
             project.Description,
             project.Status ?? ProjectStatus.Planning,
             project.Priority ?? ProjectPriority.Normal,
             project.OrganizationId,
             organization.Name,
             organization.Depth,
             organization.Type,
             organization.Path,
             organization.RootId,
             project.OwnerId,
             owner == null ? "—" : owner.FullName ?? owner.Email ?? "—",
             creator == null ? "—" : creator.FullName ?? creator.Email ?? "—",
             project.StartDate,
             project.DueDate,
             project.CreatedAt,
             project.UpdatedAt,
             project.ArchivedAt))
        .FirstOrDefaultAsync(cancellationToken);

    public async Task<List<ProjectCard>> GetByOrgAsync(
        Guid organizationId,
        bool includeDescendants,
        ProjectScope scope,
        string? search,
        CancellationToken cancellationToken = default)
    {
        // ١. النطاق — أعِد استعمال شجرة المنظّمات المبنية، لا استعلام شجرةٍ ثانياً.
        var units = includeDescendants
            ? await _organizations.GetSubtreeAsync(organizationId, cancellationToken)
            : [await _organizations.GetByIdAsync(organizationId, cancellationToken)
                ?? throw new InvalidOperationException(_text["Org_NotFound"])];

        var unitIds = units.Select(u => u.Id).ToList();
        var unitById = units.ToDictionary(u => u.Id); // الاسم والعمق على البطاقة بلا ضمّ

        var query = Live().Where(p => unitIds.Contains(p.OrganizationId!.Value));

        // ٢. التبويب — الأرشيف ليس حذفاً: ArchivedAt هو الفاصل.
        query = scope == ProjectScope.Archived
            ? query.Where(p => p.ArchivedAt != null)
            : query.Where(p => p.ArchivedAt == null);

        // ٣. البحث — اهرب من محارف LIKE وإلّا صار % من المستخدم محرفاً بدلاً.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = EscapeLike(search.Trim());
            query = query.Where(p => EF.Functions.Like(p.Name, $"%{term}%")
                                  || EF.Functions.Like(p.Code, $"%{term}%"));
        }

        // ٤. العدّ داخل الإسقاط — استعلام واحد لا استعلامٌ لكلّ بطاقة،
        //    والمهامّ الجذريّة فقط: الفرعيّة تُضاعف العدّ.
        //    المالك ضمٌّ يدويّ (لا علاقة تنقّل بين Project وUser).
        var list = await (
            from p in query
            join u in _context.Users.AsNoTracking()
                on p.OwnerId equals (Guid?)u.Id into owners
            from owner in owners.DefaultIfEmpty()
            orderby p.UpdatedAt ?? p.CreatedAt descending
            select new
            {
                p.Id,
                p.Code,
                p.Name,
                p.Description,
                p.Status,
                p.Priority,
                p.OrganizationId,
                p.OwnerId,
                p.DueDate,
                p.ArchivedAt,
                OwnerName = owner == null ? null : owner.FullName,
                TotalTasks = p.Tasks.Count(t => t.ParentTaskId == null),
                DoneTasks = p.Tasks.Count(t => t.ParentTaskId == null && t.CompletedAt != null)
            })
            .Take(200) // لا ترقيم صفحاتٍ في التصميم فلا نبنيه
            .ToListAsync(cancellationToken);

        return list.Select(row =>
        {
            var unit = unitById.GetValueOrDefault(row.OrganizationId!.Value);
            return new ProjectCard(
                row.Id,
                row.Code,
                row.Name,
                row.Description,
                row.Status ?? ProjectStatus.Planning,
                row.Priority ?? ProjectPriority.Normal,
                row.OrganizationId,
                unit?.Name,
                unit?.Depth ?? 0,
                unit?.Type ?? Models.OrgUnitTypes.Organization,
                row.OwnerId,
                row.OwnerName,
                row.DueDate,
                row.ArchivedAt,
                row.TotalTasks,
                row.DoneTasks);
        }).ToList();
    }

    public async Task<ProjectStats> GetStatsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var counts = await GetTaskStatusCountsAsync(projectId, cancellationToken);
        var overdue = await CountOverdueTasksAsync(projectId, cancellationToken);
        var teamCount = await (
            from team in _context.Teams.AsNoTracking()
            join member in _context.TeamMembers.AsNoTracking() on team.Id equals member.TeamId
            where team.ProjectId == projectId
            select member.Id).CountAsync(cancellationToken);

        return new ProjectStats(
            counts.Where(pair => pair.Key != TaskState.Cancelled).Sum(pair => pair.Value),
            counts.GetValueOrDefault(TaskState.Done),
            overdue,
            teamCount);
    }

    public async Task<Dictionary<string, int>> GetTaskStatusCountsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.Tasks.AsNoTracking()
            .Where(task => task.ProjectId == projectId && task.ParentTaskId == null)
            .GroupBy(task => task.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var actual = rows.ToDictionary(row => row.Status, row => row.Count);
        return TaskState.All.ToDictionary(status => status, status => actual.GetValueOrDefault(status));
    }

    public Task<int> CountOverdueTasksAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(global::ProjectManagmentFlow.DisplayTime.RiyadhNow());
        return _context.Tasks.AsNoTracking().CountAsync(
            task => task.ProjectId == projectId
                    && task.CompletedAt == null
                    && task.DueDate != null
                    && task.DueDate < today
                    && task.Status != TaskState.Done
                    && task.Status != TaskState.Cancelled,
            cancellationToken);
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
        _context.Projects.AsNoTracking().AnyAsync(p => p.Code == code, cancellationToken);

    public async Task<string> GetNextCodePreviewAsync(CancellationToken cancellationToken = default)
    {
        var year = global::ProjectManagmentFlow.DisplayTime.RiyadhNow().Year;
        var prefix = $"{year}-";
        var codes = await _context.Projects.AsNoTracking()
            .Where(p => p.Code.StartsWith(prefix))
            .Select(p => p.Code)
            .ToListAsync(cancellationToken);
        var sequence = codes
            .Select(code => int.TryParse(code[prefix.Length..], out var parsed) ? parsed : 0)
            .DefaultIfEmpty()
            .Max();

        return $"{year}-{sequence + 1:D3}";
    }

    public async Task<List<ProjectPerson>> GetCandidatesAsync(
        Guid rootOrganizationId,
        CancellationToken cancellationToken = default)
    {
        if (_candidateCache.TryGetValue(rootOrganizationId, out var cached)) return cached;

        var rows = await (
            from membership in _context.OrgMembers.AsNoTracking()
            join organization in _context.Organizations.AsNoTracking()
                on membership.OrganizationId equals (Guid?)organization.Id
            join user in _context.Users.AsNoTracking()
                on membership.UserId equals (Guid?)user.Id
            where organization.RootId == rootOrganizationId
               && organization.DeletedAt == null
               && membership.Status == OrgMemberStatus.Active
               && user.IsActive
            orderby user.FullName, organization.Depth descending
            select new
            {
                user.Id,
                Name = user.FullName ?? user.Email ?? "—",
                OrganizationName = organization.Name,
                OrganizationRole = membership.Role ?? OrgMemberRoles.Member
            })
            .ToListAsync(cancellationToken);

        return _candidateCache[rootOrganizationId] = rows
            .GroupBy(row => row.Id)
            .Select(group => group.First())
            .Select(row => new ProjectPerson(
                row.Id, row.Name, row.OrganizationName, row.OrganizationRole))
            .OrderBy(person => person.Name)
            .ToList();
    }

    private IQueryable<Project> Live() =>
        _context.Projects.AsNoTracking().Where(p => p.DeletedAt == null);

    /// <summary>هروب محارف LIKE في SQL Server: [ أوّلاً ثمّ % و_.</summary>
    private static string EscapeLike(string term) =>
        term.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
