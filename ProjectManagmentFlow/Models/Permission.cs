namespace ProjectManagmentFlow.Models;

public class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty; // مثال: "projects:create"
    public string Description { get; set; } = string.Empty;

    // العلاقات
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}