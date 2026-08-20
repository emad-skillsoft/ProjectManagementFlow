using System.ComponentModel.DataAnnotations;

namespace ProjectManagmentFlow.ViewModels;

public class RoleListItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public int PermissionCount { get; set; }
    public int MemberCount { get; set; }
}

public class RoleFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "RoleForm_NameRequired")]
    [StringLength(128, ErrorMessage = "RoleForm_NameTooLong")]
    [Display(Name = "RoleForm_Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(512, ErrorMessage = "RoleForm_DescriptionTooLong")]
    [Display(Name = "RoleForm_Description")]
    public string Description { get; set; } = string.Empty;

    [StringLength(128, ErrorMessage = "RoleForm_NameTooLong")]
    [Display(Name = "RoleForm_NameEn")]
    public string? NameEn { get; set; }

    [StringLength(512, ErrorMessage = "RoleForm_DescriptionTooLong")]
    [Display(Name = "RoleForm_DescriptionEn")]
    public string? DescriptionEn { get; set; }

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
