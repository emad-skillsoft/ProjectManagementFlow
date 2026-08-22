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

    public bool IsSelected { get; set; }
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

public class PermissionsPageViewModel
{
    public IReadOnlyList<RoleListItemViewModel> Roles { get; set; } = [];

    public RolePermissionsViewModel? Selected { get; set; }
}

public class RolePermissionsViewModel
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public List<PermissionChoice> Permissions { get; set; } = [];

    /// <summary>معرّفات الصلاحيات المؤشّرة في النموذج المُرسَل.</summary>
    public List<Guid> SelectedPermissionIds { get; set; } = [];

    public PermissionMatrixViewModel Matrix { get; set; } = new();

    public RolePanelViewModel Panel { get; set; } = new();
}

public class PermissionChoice
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
}

public enum PermissionCellState
{
    Denied,
    Granted,
    Mixed,
    NotApplicable
}

public class PermissionMatrixViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string ServiceLabel { get; set; } = string.Empty;
    public string RowToggleLabel { get; set; } = string.Empty;
    public string NotApplicableLabel { get; set; } = string.Empty;
    public string NotApplicableHint { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; }

    public IReadOnlyList<PermissionOperationViewModel> Operations { get; set; } = [];
    public IReadOnlyList<PermissionRowViewModel> Rows { get; set; } = [];
    public IReadOnlyList<PermissionLegendItemViewModel> Legend { get; set; } = [];
}

public class PermissionOperationViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ToggleLabel { get; set; } = string.Empty;
    public PermissionCellState State { get; set; }
}

public class PermissionRowViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Tag { get; set; }
    public string ToggleLabel { get; set; } = string.Empty;
    public string GrantedCountLabel { get; set; } = string.Empty;
    public PermissionCellState State { get; set; }
    public IReadOnlyList<PermissionCellViewModel> Cells { get; set; } = [];
}

public class PermissionCellViewModel
{
    public Guid? PermissionId { get; set; }
    public string OperationKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public PermissionCellState State { get; set; }
}

public class PermissionLegendItemViewModel
{
    public PermissionCellState State { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class RolePanelViewModel
{
    public string Label { get; set; } = string.Empty;
    public string NameLabel { get; set; } = string.Empty;
    public string NameInputId { get; set; } = "role-name";
    public string Name { get; set; } = string.Empty;
    public string PreviewTitle { get; set; } = string.Empty;
    public string OperationCountLabel { get; set; } = string.Empty;
    public int OperationCount { get; set; }
    public string EmptyPreviewLabel { get; set; } = string.Empty;
    public string SaveLabel { get; set; } = string.Empty;
    public string ResetLabel { get; set; } = string.Empty;
    public bool IsSaveDisabled { get; set; }
    public IReadOnlyList<RolePreviewLineViewModel> PreviewLines { get; set; } = [];
}

public class RolePreviewLineViewModel
{
    public string Service { get; set; } = string.Empty;
    public string Operations { get; set; } = string.Empty;
}
