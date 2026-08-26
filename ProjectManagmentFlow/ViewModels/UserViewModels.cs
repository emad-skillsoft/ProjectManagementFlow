namespace ProjectManagmentFlow.ViewModels;

public class UserListItemViewModel
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public List<string> Roles { get; set; } = [];
}

public class UserRolesViewModel
{
    public Guid UserId { get; set; }

    /// <summary>المنظّمة التي فُتحت منها الشاشة — للعودة إلى جدول أعضائها.</summary>
    public Guid OrganizationId { get; set; }

    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsSelf { get; set; }
    public List<RoleChoice> Roles { get; set; } = [];

    /// <summary>معرّفات الأدوار المؤشّرة في النموذج المُرسَل.</summary>
    public List<Guid> SelectedRoleIds { get; set; } = [];
}

public class RoleChoice
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsAssigned { get; set; }
}
