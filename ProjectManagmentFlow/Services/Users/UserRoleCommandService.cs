using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services.Security;
using ProjectManagmentFlow.Services.Users;

namespace ProjectManagmentFlow.Services.Users;

public class UserRoleCommandService : IUserRoleCommandService
{
    private readonly AppDbContext _context;
    private readonly ISecurityStampService _securityStamps;

    public UserRoleCommandService(AppDbContext context, ISecurityStampService securityStamps)
    {
        _context = context;
        _securityStamps = securityStamps;
    }

    public async Task<bool> AssignRolesToUserAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        if (roleIds.Count == 0) return false;
        if (!await _context.Users.AnyAsync(u => u.Id == userId, cancellationToken)) return false;

        // التحقّق من وجود الأدوار مسبقاً بدل ترك قيد المفتاح الأجنبي يفشل بخطأ غامض.
        var knownRoleIds = await _context.Roles
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (knownRoleIds.Count != roleIds.Distinct().Count()) return false;

        var existingRoleIds = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        var newRoles = knownRoleIds
            .Except(existingRoleIds)
            .Select(roleId => new UserRole { UserId = userId, RoleId = roleId })
            .ToList();

        if (newRoles.Count == 0) return true;

        await _context.UserRoles.AddRangeAsync(newRoles, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await _securityStamps.RefreshUsersAsync([userId], cancellationToken);
        return true;
    }

    public async Task<bool> RemoveRolesFromUserAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default)
    {
        if (roleIds.Count == 0) return false;

        var targetRoles = await _context.UserRoles
            .Where(ur => ur.UserId == userId && roleIds.Contains(ur.RoleId))
            .ToListAsync(cancellationToken);

        if (targetRoles.Count == 0) return false;

        _context.UserRoles.RemoveRange(targetRoles);
        await _context.SaveChangesAsync(cancellationToken);
        await _securityStamps.RefreshUsersAsync([userId], cancellationToken);
        return true;
    }
}
