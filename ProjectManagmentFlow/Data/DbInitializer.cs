using System.Reflection;
using Microsoft.AspNetCore.Identity;
using ProjectManagmentFlow.Authorization;
using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Data;

/// <summary>
/// زراعة بيانات تجريبيّة للتطوير فقط. تُستدعى من Program.cs داخل حارس البيئة،
/// </summary>
public static class DbInitializer
{
    private const string DemoPassword = "Aa123123#";

    public static async Task SeedAsync(AppDbContext context, IPasswordHasher<User> passwordHasher)
    {
        // تطبيق الهجرات — لا EnsureCreated، لأنّه يتجاوز سجلّ الهجرات
        // فتفشل أوّل هجرة لاحقة برسالة "الجدول موجود".
        await context.Database.MigrateAsync();

        // 1. الصلاحيات (Permissions)
        var permRolesView = await EnsurePermissionAsync(context, PermissionNames.RolesView, "عرض الأدوار وصلاحياتها");
        var permRolesCreate = await EnsurePermissionAsync(context, PermissionNames.RolesCreate, "إنشاء أدوار جديدة");
        var permRolesEdit = await EnsurePermissionAsync(context, PermissionNames.RolesEdit, "تعديل الأدوار ومنح صلاحياتها وسحبها");
        var permRolesDelete = await EnsurePermissionAsync(context, PermissionNames.RolesDelete, "حذف الأدوار");
        var permUsersView = await EnsurePermissionAsync(context, PermissionNames.UsersView, "عرض المستخدمين");
        var permUsersEdit = await EnsurePermissionAsync(context, PermissionNames.UsersEdit, "إسناد الأدوار للمستخدمين وسحبها");
        var permOrgsView = await EnsurePermissionAsync(context, PermissionNames.OrganizationsView, "عرض المنظّمات");
        var permProjectsView = await EnsurePermissionAsync(context, PermissionNames.ProjectsView, "عرض المشاريع");
        var permTasksView = await EnsurePermissionAsync(context, PermissionNames.TasksView, "عرض المهام");
        var permTeamsView = await EnsurePermissionAsync(context, PermissionNames.TeamsView, "عرض الفرق");

        // 2. الأدوار (Roles)
        var adminRole = await EnsureRoleAsync(context, "Admin", "مدير النظام بكامل الصلاحيات", isSystem: true);
        var memberRole = await EnsureRoleAsync(context, "Member", "عضو باطّلاع فقط على الأدوار والمستخدمين", isSystem: true);

        await context.SaveChangesAsync();

        // 3. ربط الصلاحيات بالأدوار (RolePermissions)
        await EnsureRolePermissionAsync(context, adminRole.Id, permRolesView.Id);
        await EnsureRolePermissionAsync(context, adminRole.Id, permRolesCreate.Id);
        await EnsureRolePermissionAsync(context, adminRole.Id, permRolesEdit.Id);
        await EnsureRolePermissionAsync(context, adminRole.Id, permRolesDelete.Id);
        await EnsureRolePermissionAsync(context, adminRole.Id, permUsersView.Id);
        await EnsureRolePermissionAsync(context, adminRole.Id, permUsersEdit.Id);
        await EnsureRolePermissionAsync(context, adminRole.Id, permOrgsView.Id);
        await EnsureRolePermissionAsync(context, adminRole.Id, permProjectsView.Id);
        await EnsureRolePermissionAsync(context, adminRole.Id, permTasksView.Id);
        await EnsureRolePermissionAsync(context, adminRole.Id, permTeamsView.Id);
        await EnsureRolePermissionAsync(context, memberRole.Id, permRolesView.Id);
        await EnsureRolePermissionAsync(context, memberRole.Id, permUsersView.Id);

        // 4. مستخدمون تجريبيّون  (Users)
        var adminUser = await EnsureUserAsync(context, passwordHasher, "admin@example.com", "عمر السلمي (Admin)");
        var regularUser = await EnsureUserAsync(context, passwordHasher, "user@example.com", "مستخدم عادي");

        await context.SaveChangesAsync();

        // 5. ربط المستخدمين بالأدوار (UserRoles)
        await EnsureUserRoleAsync(context, adminUser.Id, adminRole.Id);
        await EnsureUserRoleAsync(context, regularUser.Id, memberRole.Id);

        await context.SaveChangesAsync();

        await PruneUnknownPermissionsAsync(context);
    }

    private static async Task PruneUnknownPermissionsAsync(AppDbContext context)
    {
        var known = typeof(PermissionNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        var obsolete = await context.Permissions
            .Where(permission => !known.Contains(permission.Name))
            .ToListAsync();

        if (obsolete.Count == 0) return;

        var obsoleteIds = obsolete.Select(permission => permission.Id).ToList();

        var assignments = await context.RolePermissions
            .Where(link => obsoleteIds.Contains(link.PermissionId))
            .ToListAsync();

        context.RolePermissions.RemoveRange(assignments);
        context.Permissions.RemoveRange(obsolete);

        await context.SaveChangesAsync();
    }

    private static async Task<Permission> EnsurePermissionAsync(AppDbContext context, string name, string description)
    {
        var permission = await context.Permissions.FirstOrDefaultAsync(p => p.Name == name);
        if (permission is not null) return permission;

        permission = new Permission { Name = name, Description = description };
        context.Permissions.Add(permission);
        return permission;
    }

    private static async Task<Role> EnsureRoleAsync(
        AppDbContext context, string name, string description, bool isSystem = false)
    {
        var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == name);
        if (role is not null)
        {
            // تصحيح ذاتيّ لقاعدة زُرعت قبل إضافة IsSystem.
            role.IsSystem = isSystem;
            return role;
        }

        role = new Role { Name = name, Description = description, IsSystem = isSystem };
        context.Roles.Add(role);
        return role;
    }

    private static async Task<User> EnsureUserAsync(
        AppDbContext context,
        IPasswordHasher<User> passwordHasher,
        string email,
        string fullName)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is not null) return user;

        user = new User
        {
            Email = email,
            FullName = fullName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, DemoPassword);

        context.Users.Add(user);
        return user;
    }

    private static async Task EnsureRolePermissionAsync(AppDbContext context, Guid roleId, Guid permissionId)
    {
        var exists = await context.RolePermissions
            .AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);

        if (!exists)
        {
            context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
        }
    }

    private static async Task EnsureUserRoleAsync(AppDbContext context, Guid userId, Guid roleId)
    {
        var exists = await context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (!exists)
        {
            context.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
        }
    }
}
