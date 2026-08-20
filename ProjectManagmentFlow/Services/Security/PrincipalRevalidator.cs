using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Services.Users;

namespace ProjectManagmentFlow.Services.Security;

/// <summary>
/// يعيد التحقّق من الكوكي دوريّاً مقابل قاعدة البيانات: يبطل الجلسة إذا حُذف المستخدم
/// أو عُطّل أو تغيّر ختم الأمان، ويعيد بناء الصلاحيات حتّى لا تبقى مجمّدة طوال عمر الكوكي.
/// </summary>
public class PrincipalRevalidator
{
    /// <summary>الفترة الافتراضيّة بين عمليّات إعادة التحقّق من قاعدة البيانات.</summary>
    public static readonly TimeSpan DefaultValidationInterval = TimeSpan.FromMinutes(15);

    public static async Task ValidateAsync(CookieValidatePrincipalContext context, TimeSpan validationInterval)
    {
        var issuedUtc = context.Properties.IssuedUtc;
        if (issuedUtc.HasValue && DateTimeOffset.UtcNow - issuedUtc.Value < validationInterval)
        {
            return;
        }

        var services = context.HttpContext.RequestServices;
        var dbContext = services.GetRequiredService<AppDbContext>();
        var principalFactory = services.GetRequiredService<IUserPrincipalFactory>();

        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            await RejectAsync(context);
            return;
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null || !user.IsActive)
        {
            await RejectAsync(context);
            return;
        }

        var stamp = context.Principal?.FindFirstValue(UserPrincipalFactory.SecurityStampClaimType);
        if (!string.Equals(stamp, user.SecurityStamp, StringComparison.Ordinal))
        {
            await RejectAsync(context);
            return;
        }

        // إعادة بناء الهويّة بصلاحيات محدّثة، وتجديد ختم الإصدار لبدء فترة تحقّق جديدة.
        context.ReplacePrincipal(await principalFactory.CreateAsync(user));
        context.Properties.IssuedUtc = DateTimeOffset.UtcNow;
        context.ShouldRenew = true;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
