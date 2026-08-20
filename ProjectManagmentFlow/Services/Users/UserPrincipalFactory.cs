using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Users;

/// <summary>
/// المصدر الوحيد لبناء هويّة المستخدم: يُستخدم عند تسجيل الدخول وعند إعادة التحقّق الدوريّة
/// من الكوكي، حتّى لا تتفرّع طريقتان لتوليد المطالبات (Claims).
/// </summary>
/// <remarks>
/// لا تُضاف مطالبات <c>ClaimTypes.Role</c> عمداً: الدور وسيلة تجميع تُدار من لوحة الأدوار،
/// أمّا قرار التفويض فيُبنى على الصلاحية وحدها. إضافتها تُفعّل <c>User.IsInRole(...)</c>
/// فيتسرّب اسم الدور إلى الكود، وعندها تكسر إعادةُ تسمية دور التفويضَ بصمت
/// ويصير تغيير السياسة محتاجاً إلى نشر جديد.
/// </remarks>
public interface IUserPrincipalFactory
{
    Task<ClaimsPrincipal> CreateAsync(User user, CancellationToken cancellationToken = default);
}

public class UserPrincipalFactory : IUserPrincipalFactory
{
    /// <summary>نوع المطالبة الحاملة لاسم الصلاحية.</summary>
    public const string PermissionClaimType = "Permission";

    /// <summary>نوع المطالبة الحاملة لرمز الأمان.</summary>
    public const string SecurityStampClaimType = "SecurityStamp";

    private readonly AppDbContext _context;

    public UserPrincipalFactory(AppDbContext context) => _context = context;

    public async Task<ClaimsPrincipal> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        var permissions = await _context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToListAsync(cancellationToken);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName ?? user.Email ?? user.Id.ToString()),
            new(SecurityStampClaimType, user.SecurityStamp)
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        claims.AddRange(permissions.Select(permission => new Claim(PermissionClaimType, permission)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}
