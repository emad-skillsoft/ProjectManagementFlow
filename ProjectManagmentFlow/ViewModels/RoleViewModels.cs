using System.ComponentModel.DataAnnotations;

namespace ProjectManagmentFlow.ViewModels;

public class RoleListItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PermissionCount { get; set; }
    public int MemberCount { get; set; }
}

public class RoleFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "اسم الدور مطلوب")]
    [StringLength(128, ErrorMessage = "اسم الدور طويل جداً")]
    [Display(Name = "اسم الدور")]
    public string Name { get; set; } = string.Empty;

    [StringLength(512, ErrorMessage = "الوصف طويل جداً")]
    [Display(Name = "الوصف")]
    public string Description { get; set; } = string.Empty;

    public bool IsNew => Id is null;
}

public class RolePermissionsViewModel
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public List<PermissionChoice> Permissions { get; set; } = [];

    /// <summary>معرّفات الصلاحيات المؤشّرة في النموذج المُرسَل.</summary>
    public List<Guid> SelectedPermissionIds { get; set; } = [];
}

public class PermissionChoice
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
}
