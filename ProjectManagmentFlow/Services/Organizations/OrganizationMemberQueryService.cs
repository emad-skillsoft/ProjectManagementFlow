using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Organizations;

public class OrganizationMemberQueryService : IOrganizationMemberQueryService
{
    private readonly AppDbContext _context;

    public OrganizationMemberQueryService(AppDbContext context) => _context = context;

    public Task<List<OrgMember>> GetMembersByOrgAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        _context.OrgMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.Status == OrgMemberStatus.Active)
            // ترتيب الرتبة لا الأبجديّة: المالك ثمّ المدير ثمّ العضو.
            .OrderBy(m => m.Role == OrgMemberRoles.Owner ? 0
                        : m.Role == OrgMemberRoles.Admin ? 1 : 2)
            .ThenBy(m => m.JoinedAt)
            .ToListAsync(cancellationToken);

    public Task<List<OrgMember>> GetPendingInvitesByOrgAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        _context.OrgMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.Status == OrgMemberStatus.Pending)
            .ToListAsync(cancellationToken);

    public Task<string?> GetMemberRoleAsync(
        Guid organizationId, Guid userId, CancellationToken cancellationToken = default) =>
        _context.OrgMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId
                     && m.UserId == userId
                     && m.Status == OrgMemberStatus.Active)
            .Select(m => m.Role)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> CountMembersAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        _context.OrgMembers
            .CountAsync(m => m.OrganizationId == organizationId
                          && m.Status == OrgMemberStatus.Active, cancellationToken);

    public Task<List<OrgMember>> GetInvitesByUserAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        _context.OrgMembers.AsNoTracking()
            .Include(m => m.Organization)
            .Where(m => m.UserId == userId
                     && m.Status == OrgMemberStatus.Pending
                     && m.Organization!.DeletedAt == null)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// أعضاء الأجداد — «موروَثون» في لوحة الوحدة: نشِطون في وحدةٍ أعلى منها،
    /// وليس لهم عضويّة مباشرة هنا. الاسم واسم الوحدة الموروثةُ منهما معاً.
    /// </summary>
    public async Task<List<InheritedMember>> GetInheritedMembersAsync(
        Guid unitId, CancellationToken cancellationToken = default)
    {
        var unit = await _context.Organizations.AsNoTracking()
            .Where(o => o.Id == unitId && o.DeletedAt == null)
            .Select(o => new { o.RootId, o.Path })
            .FirstOrDefaultAsync(cancellationToken);
        if (unit is null) return [];

        // الجدّ من يكون مساره بادئةً لمسار الوحدة — لا من يكون مساره أقصر.
        // الأقصر يشمل الفروع الشقيقة، فتُعرض عضويّاتها وكأنّها سلسلة قيادة.
        var ancestors = await _context.Organizations.AsNoTracking()
            .Where(o => o.RootId == unit.RootId
                        && o.DeletedAt == null
                        && o.Id != unitId
                        && unit.Path.StartsWith(o.Path))
            .Select(o => new { o.Id, o.Name, o.Depth })
            .OrderBy(o => o.Depth)
            .ToListAsync(cancellationToken);

        if (ancestors.Count == 0) return [];

        var ancestorIds = ancestors.Select(a => a.Id).ToList();
        var nameByUnit = ancestors.ToDictionary(a => a.Id, a => a.Name);

        return await (
            from member in _context.OrgMembers.AsNoTracking()
            join user in _context.Users.AsNoTracking()
                on member.UserId equals user.Id
            where ancestorIds.Contains(member.OrganizationId!.Value)
                   && member.Status == OrgMemberStatus.Active
                   && user.IsActive
                   && !_context.OrgMembers.Any(direct =>
                       direct.OrganizationId == unitId
                       && direct.UserId == member.UserId
                       && direct.Status == OrgMemberStatus.Active)
            orderby user.FullName, member.OrganizationId
            select new InheritedMember(
                user.Id,
                user.FullName ?? user.Email ?? string.Empty,
                user.Email ?? string.Empty,
                member.Role!,
                member.OrganizationId!.Value,
                nameByUnit[member.OrganizationId!.Value]))
            .ToListAsync(cancellationToken);
    }
}
