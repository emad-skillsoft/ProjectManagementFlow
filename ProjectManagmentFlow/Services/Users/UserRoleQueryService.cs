using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services.Users;

namespace ProjectManagmentFlow.Services.Users;

public class UserRoleQueryService : IUserRoleQueryService
{
    private readonly AppDbContext _context;

    public UserRoleQueryService(AppDbContext context) => _context = context;

    public async Task<List<Role>> GetRolesByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _context.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

    public async Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToListAsync(cancellationToken);
}
