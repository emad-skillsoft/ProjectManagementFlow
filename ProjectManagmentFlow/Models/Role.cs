namespace ProjectManagmentFlow.Models;

public class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;


    public string? NameEn { get; set; }
    public string? DescriptionEn { get; set; }


    public bool IsSystem { get; set; }

    // العلاقات
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}