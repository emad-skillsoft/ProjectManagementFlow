using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Roles;

public class RoleQueryService : IRoleQueryService
{
    private readonly AppDbContext _context;

    public RoleQueryService(AppDbContext context) => _context = context;

    public async Task<List<RoleSummary>> GetSummariesAsync(CancellationToken cancellationToken = default)
        => await _context.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleSummary(
                r.Id,
                r.Name,
                r.NameEn,
                r.Description,
                r.DescriptionEn,
                r.IsSystem,
                r.RolePermissions.Count,
                r.UserRoles.Count))
            .ToListAsync(cancellationToken);

    public async Task<Role?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
        => await _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

    public async Task<List<Role>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(cancellationToken);

    public async Task<List<Permission>> GetPermissionsByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
        => await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<bool> HasPermissionAsync(Guid roleId, string permissionName, CancellationToken cancellationToken = default)
        => await _context.RolePermissions
            .AnyAsync(rp => rp.RoleId == roleId && rp.Permission.Name == permissionName, cancellationToken);

}
