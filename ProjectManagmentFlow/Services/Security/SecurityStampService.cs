using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;

namespace ProjectManagmentFlow.Services.Security;


public class SecurityStampService : ISecurityStampService
{
    private readonly AppDbContext _context;

    public SecurityStampService(AppDbContext context) => _context = context;

    public async Task RefreshUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return;

        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RefreshRoleMembersAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var userIds = await _context.UserRoles
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

        await RefreshUsersAsync(userIds, cancellationToken);
    }
}
