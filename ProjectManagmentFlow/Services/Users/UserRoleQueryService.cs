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

    public async Task<Dictionary<Guid, List<Role>>> GetRolesByUsersAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return [];

        var rows = await _context.UserRoles
            .AsNoTracking()
            .Where(ur => userIds.Contains(ur.UserId))
            .Select(ur => new { ur.UserId, ur.Role })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.Role).OrderBy(role => role.Name).ToList());
    }

    public async Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToListAsync(cancellationToken);
}
