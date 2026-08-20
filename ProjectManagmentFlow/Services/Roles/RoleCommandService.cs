using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services.Security;

namespace ProjectManagmentFlow.Services.Roles;

public class RoleCommandService : IRoleCommandService
{
    private readonly AppDbContext _context;
    private readonly ISecurityStampService _securityStamps;

    public RoleCommandService(AppDbContext context, ISecurityStampService securityStamps)
    {
        _context = context;
        _securityStamps = securityStamps;
    }

    public async Task<Role> CreateAsync(string name, string description, CancellationToken cancellationToken = default)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length == 0)
        {
            throw new ArgumentException("اسم الدور مطلوب.", nameof(name));
        }

        if (await _context.Roles.AnyAsync(r => r.Name == trimmedName, cancellationToken))
        {
            throw new InvalidOperationException($"يوجد دور باسم \"{trimmedName}\" مسبقاً.");
        }

        var role = new Role { Name = trimmedName, Description = description ?? string.Empty };
        _context.Roles.Add(role);
        await _context.SaveChangesAsync(cancellationToken);
        return role;
    }

    public async Task<bool> UpdateAsync(Guid roleId, string name, string description, CancellationToken cancellationToken = default)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length == 0) return false;

        var role = await _context.Roles.FindAsync([roleId], cancellationToken);
        if (role is null) return false;

        // منع الاصطدام بالفهرس الفريد على الاسم برسالة قاعدة بيانات غامضة.
        var nameTaken = await _context.Roles
            .AnyAsync(r => r.Id != roleId && r.Name == trimmedName, cancellationToken);
        if (nameTaken) return false;

        role.Name = trimmedName;
        role.Description = description ?? string.Empty;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles.FindAsync([roleId], cancellationToken);
        if (role is null) return false;

        // تجديد أختام أعضاء الدور قبل حذفه، وإلّا بقيت صلاحياته في كوكياتهم.
        await _securityStamps.RefreshRoleMembersAsync(roleId, cancellationToken);

        _context.Roles.Remove(role);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> AssignPermissionsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken = default)
    {
        if (permissionIds.Count == 0) return false;
        if (!await _context.Roles.AnyAsync(r => r.Id == roleId, cancellationToken)) return false;

        // التحقّق من وجود الصلاحيات مسبقاً بدل ترك قيد المفتاح الأجنبي يفشل بخطأ غامض.
        var knownPermissionIds = await _context.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (knownPermissionIds.Count != permissionIds.Distinct().Count()) return false;

        var existingPermissionIds = await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync(cancellationToken);

        var newPermissions = knownPermissionIds
            .Except(existingPermissionIds)
            .Select(permissionId => new RolePermission { RoleId = roleId, PermissionId = permissionId })
            .ToList();

        if (newPermissions.Count == 0) return true;

        await _context.RolePermissions.AddRangeAsync(newPermissions, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await _securityStamps.RefreshRoleMembersAsync(roleId, cancellationToken);
        return true;
    }

    public async Task<bool> RevokePermissionsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken = default)
    {
        if (permissionIds.Count == 0) return false;

        var targetPermissions = await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId && permissionIds.Contains(rp.PermissionId))
            .ToListAsync(cancellationToken);

        if (targetPermissions.Count == 0) return false;

        _context.RolePermissions.RemoveRange(targetPermissions);
        await _context.SaveChangesAsync(cancellationToken);
        await _securityStamps.RefreshRoleMembersAsync(roleId, cancellationToken);
        return true;
    }
}
