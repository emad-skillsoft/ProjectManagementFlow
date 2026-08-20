namespace ProjectManagmentFlow.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? PasswordHash { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// رمز يتغيّر كلّما تغيّرت بيانات الأمان (الأدوار، الصلاحيات، كلمة المرور).
    /// اختلافه عن الرمز المحفوظ في الكوكي يُبطل الجلسة فوراً.
    /// </summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public int AccessFailedCount { get; set; }

    public DateTime? LockoutEndUtc { get; set; }

    public DateTime? LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // العلاقات
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
