using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Organizations;

public class OrganizationQueryService : IOrganizationQueryService
{
    private const char Separator = '/';

    private readonly AppDbContext _context;

    public OrganizationQueryService(AppDbContext context) => _context = context;

    public Task<Organization?> GetByIdAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        Live().FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

    public Task<List<Organization>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Live().OrderBy(o => o.Path).ToListAsync(cancellationToken);

    public Task<List<Organization>> GetOrganizationsByUserAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        Live()
            .Where(o => o.Members.Any(m => m.UserId == userId && m.Status == OrgMemberStatus.Active))
            .OrderBy(o => o.Path)
            .ToListAsync(cancellationToken);

    public async Task<List<Organization>> GetAncestorsAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        var path = await Live()
            .Where(o => o.Id == organizationId)
            .Select(o => o.Path)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(path)) return [];

        // المسار يحمل معرّفات السلسلة كاملةً، فاستعلام واحد يكفي مهما عمقت الشجرة.
        var ids = path
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => Guid.ParseExact(segment, "N"))
            .ToList();

        return await Live()
            .Where(o => ids.Contains(o.Id))
            .OrderBy(o => o.Depth)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Organization>> GetChildrenAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        Live()
            .Where(o => o.ParentId == organizationId)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);

    public async Task<List<Organization>> GetSubtreeAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        var scope = await GetScopePathAsync(organizationId, cancellationToken);
        if (scope is null) return [];

        return await Live()
            .Where(o => o.Path.StartsWith(scope))
            .OrderBy(o => o.Depth)
            .ThenBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Organization>> GetRootsAsync(CancellationToken cancellationToken = default) =>
        Live()
            .Where(o => o.ParentId == null)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);

    public async Task<List<string>> GetScopePathsByUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var paths = await Live()
            .Where(o => o.Members.Any(m => m.UserId == userId && m.Status == OrgMemberStatus.Active))
            .Select(o => o.Path)
            .ToListAsync(cancellationToken);

        return OrgScope.Outermost(paths);
    }

    public Task<int> CountChildrenAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        Live().CountAsync(o => o.ParentId == organizationId, cancellationToken);

    public async Task<bool> IsDescendantOfAsync(
        Guid organizationId, Guid ancestorId, CancellationToken cancellationToken = default)
    {
        if (organizationId == ancestorId) return false;

        var paths = await Live()
            .Where(o => o.Id == organizationId || o.Id == ancestorId)
            .Select(o => new { o.Id, o.Path })
            .ToListAsync(cancellationToken);

        var self = paths.FirstOrDefault(p => p.Id == organizationId)?.Path;
        var ancestor = paths.FirstOrDefault(p => p.Id == ancestorId)?.Path;

        return self is not null
            && ancestor is not null
            && self.StartsWith(ancestor, StringComparison.Ordinal);
    }

    public async Task<string?> GetScopePathAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        await Live()
            .Where(o => o.Id == organizationId)
            .Select(o => o.Path)
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<Organization> Live() =>
        _context.Organizations.AsNoTracking().Where(o => o.DeletedAt == null);
}
