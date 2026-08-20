using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services.Security;
using ProjectManagmentFlow.Services.Users;

namespace ProjectManagmentFlow.Services.Security;

public enum LoginResult
{
    Success,
    InvalidCredentials,
    Disabled,
    LockedOut
}

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task LogoutAsync();
}

public class AuthService : IAuthService
{
    /// <summary>عدد المحاولات الفاشلة المتتالية قبل الإيقاف المؤقّت.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>مدّة الإيقاف المؤقّت بعد استنفاد المحاولات.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IUserPrincipalFactory _principalFactory;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IPasswordHasher<User> passwordHasher,
        IUserPrincipalFactory principalFactory,
        ILogger<AuthService> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _passwordHasher = passwordHasher;
        _principalFactory = principalFactory;
        _logger = logger;
    }

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = (email ?? string.Empty).Trim();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            // تجزئة وهميّة حتّى يتساوى زمن الاستجابة بين بريد موجود وآخر غير موجود،
            // فلا يصلح الردّ لاستكشاف الحسابات المسجّلة.
            _passwordHasher.HashPassword(new User(), password ?? string.Empty);
            return LoginResult.InvalidCredentials;
        }

        if (user.LockoutEndUtc is { } lockoutEnd && lockoutEnd > DateTime.UtcNow)
        {
            _logger.LogWarning("محاولة دخول لحساب موقوف مؤقّتاً: {UserId}", user.Id);
            return LoginResult.LockedOut;
        }

        if (!VerifyPassword(user, password ?? string.Empty))
        {
            await RegisterFailedAttemptAsync(user, cancellationToken);
            return LoginResult.InvalidCredentials;
        }

        // فحص التفعيل يأتي بعد التحقّق من كلمة المرور حتّى لا يكشف الردّ
        // حالة الحساب لمن لا يملك بياناته.
        if (!user.IsActive)
        {
            _logger.LogWarning("محاولة دخول لحساب غير مفعّل: {UserId}", user.Id);
            return LoginResult.Disabled;
        }

        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;
        user.LastSeenAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("لا يمكن تسجيل الدخول خارج سياق طلب HTTP.");

        var principal = await _principalFactory.CreateAsync(user, cancellationToken);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow });

        return LoginResult.Success;
    }

    public async Task LogoutAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null) return;

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private bool VerifyPassword(User user, string password)
    {
        if (string.IsNullOrEmpty(user.PasswordHash)) return false;

        PasswordVerificationResult result;
        try
        {
            result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        }
        catch (FormatException)
        {
            // تجزئة مخزّنة بصيغة غير معروفة (مثل كلمة مرور نصّيّة قديمة) — تُرفض ولا تُقبل كتطابق.
            _logger.LogError("صيغة تجزئة كلمة المرور غير صالحة للمستخدم {UserId}.", user.Id);
            return false;
        }

        if (result == PasswordVerificationResult.Failed) return false;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
            user.UpdatedAt = DateTime.UtcNow;
        }

        return true;
    }

    private async Task RegisterFailedAttemptAsync(User user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount++;

        if (user.AccessFailedCount >= MaxFailedAttempts)
        {
            user.LockoutEndUtc = DateTime.UtcNow.Add(LockoutDuration);
            user.AccessFailedCount = 0;
            _logger.LogWarning("إيقاف مؤقّت للحساب {UserId} بعد {Attempts} محاولات فاشلة.", user.Id, MaxFailedAttempts);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
