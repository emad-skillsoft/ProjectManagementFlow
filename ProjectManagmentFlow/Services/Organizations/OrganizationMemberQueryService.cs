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
}
