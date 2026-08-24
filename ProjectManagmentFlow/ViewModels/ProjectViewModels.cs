using Microsoft.AspNetCore.Mvc;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.ModelBinding;
using ProjectManagmentFlow.Services.Projects;

namespace ProjectManagmentFlow.ViewModels;

public class UnitTypeBadgeViewModel
{
    public string Label { get; set; } = string.Empty;
    public bool IsRoot { get; set; }
}

public class UnitScopeOptionViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

public class UnitScopeSwitchViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
    public IReadOnlyList<UnitScopeOptionViewModel> Options { get; set; } = [];
}

public class StatusFilterViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool IsSelected { get; set; }
    public string Href { get; set; } = string.Empty;
}

public class ProjectCardViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string StatusClass { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public string PriorityLabel { get; set; } = string.Empty;
    public string PriorityClass { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string UnitTypeLabel { get; set; } = string.Empty;
    public bool UnitIsRoot { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerInitial { get; set; } = string.Empty;
    public string DueLabel { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    public string ProgressLabel { get; set; } = string.Empty;
    public bool HasTasks { get; set; }
    public int Percent { get; set; }
    public string Href { get; set; } = string.Empty;
}

public class ProjectsIndexViewModel
{
    public required Organization Unit { get; init; }
    public required IReadOnlyList<Organization> Ancestors { get; init; }
    public bool HasUnit { get; set; } = true;
    public bool IncludeDescendants { get; set; }
    public ProjectScope Scope { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
    public UnitScopeSwitchViewModel ScopeSwitch { get; set; } = new();
    public List<StatusFilterViewModel> Filters { get; set; } = [];
    public List<ProjectCardViewModel> Cards { get; set; } = [];
    public bool CanCreate { get; set; }
}

public class ProjectCreateInput
{
    public Guid UnitId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerId { get; set; }
    public string Status { get; set; } = ProjectStatus.Planning;
    public string Priority { get; set; } = ProjectPriority.Normal;
    [ModelBinder(BinderType = typeof(IsoDateOnlyModelBinder))]
    public DateOnly? StartDate { get; set; }

    [ModelBinder(BinderType = typeof(IsoDateOnlyModelBinder))]
    public DateOnly? DueDate { get; set; }
    public List<Guid> SelectedMemberIds { get; set; } = [];
    public Dictionary<Guid, string> MemberRoles { get; set; } = [];
}

public class ProjectCreateOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

public class ProjectCreateUnitViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public short Depth { get; set; }
    public bool IsRoot { get; set; }
    public bool IsSelected { get; set; }
    public string Href { get; set; } = string.Empty;
}

public class ProjectCreatePersonViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Initial { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string OrganizationRoleLabel { get; set; } = string.Empty;
    public bool IsOwner { get; set; }
    public bool IsSelected { get; set; }
    public string SelectedRole { get; set; } = TeamMemberRoles.Member;
}

public class ProjectCreateStepOneViewModel
{
    public ProjectCreateInput Input { get; set; } = new();
    public List<ProjectCreateUnitViewModel> Units { get; set; } = [];
    public List<ProjectCreatePersonViewModel> Owners { get; set; } = [];
    public List<ProjectCreateOptionViewModel> Statuses { get; set; } = [];
    public List<ProjectCreateOptionViewModel> Priorities { get; set; } = [];
    public string UnitPathLabel { get; set; } = string.Empty;
    public string CodePreview { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public Dictionary<string, string> FieldErrors { get; set; } = [];
    public string? Error { get; set; }
}

public class ProjectCreateStepTwoViewModel
{
    public ProjectCreateInput Input { get; set; } = new();
    public string UnitName { get; set; } = string.Empty;
    public string UnitPathLabel { get; set; } = string.Empty;
    public string CodePreview { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public List<ProjectCreatePersonViewModel> Candidates { get; set; } = [];
    public List<ProjectCreateOptionViewModel> Roles { get; set; } = [];
    public Dictionary<string, string> FieldErrors { get; set; } = [];
    public string? Error { get; set; }
}

public class ProjectTabViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Href { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public bool IsAvailable { get; set; }
}

public class ProjectHeaderViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public string UnitHref { get; set; } = string.Empty;
    public UnitTypeBadgeViewModel UnitType { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public string StatusClass { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string PriorityClass { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string? LeadName { get; set; }
    public string StartLabel { get; set; } = string.Empty;
    public string DueLabel { get; set; } = string.Empty;
    public string TeamLabel { get; set; } = string.Empty;
    public bool MayEdit { get; set; }
    public List<ProjectTabViewModel> Tabs { get; set; } = [];
}

public class TaskStatusBarViewModel
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public int WidthPercent { get; set; }
    public string DotClass { get; set; } = string.Empty;
    public string BarClass { get; set; } = string.Empty;
}

public class ProjectFactViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class ProjectDetailViewModel
{
    public ProjectHeaderViewModel Header { get; set; } = new();
    public string? Description { get; set; }
    public List<TaskStatusBarViewModel> StatusBars { get; set; } = [];
    public string TasksSummary { get; set; } = string.Empty;
    public List<ProjectFactViewModel> Facts { get; set; } = [];
    public string OwnerInitials { get; set; } = string.Empty;
    public string OwnerDepartment { get; set; } = string.Empty;
    public string OwnerRoleDescription { get; set; } = string.Empty;
    public bool MayArchive { get; set; }
}

public class ProjectLeadCardViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string BadgeLabel { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

public class ProjectTeamMemberViewModel
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool IsProjectOwner { get; set; }
    public string Department { get; set; } = string.Empty;
    public int OpenTasks { get; set; }
}

public class ProjectTeamViewModel
{
    public ProjectHeaderViewModel Header { get; set; } = new();
    public Guid ProjectId { get; set; }
    public List<ProjectLeadCardViewModel> LeadCards { get; set; } = [];
    public List<ProjectTeamMemberViewModel> Members { get; set; } = [];
    public List<ProjectCreateOptionViewModel> Roles { get; set; } = [];
    public List<ProjectCreatePersonViewModel> Candidates { get; set; } = [];
    public bool MayEdit { get; set; }
    public string? Error { get; set; }
    public string? Saved { get; set; }
}

public class ProjectActivityItemViewModel
{
    public string Title { get; set; } = string.Empty;
    public string TimeLabel { get; set; } = string.Empty;
    public string TimeIso { get; set; } = string.Empty;
    public string ToneClass { get; set; } = string.Empty;
}

public class ProjectActivityViewModel
{
    public ProjectHeaderViewModel Header { get; set; } = new();
    public List<ProjectActivityItemViewModel> Items { get; set; } = [];
}

public static class ProjectPresentation
{
    public static string StatusClass(string status) => status switch
    {
        ProjectStatus.Active => "ds-status-dot ds-status-dot--success",
        ProjectStatus.OnHold => "ds-status-dot ds-status-dot--warning",
        ProjectStatus.Done => "ds-status-dot ds-status-dot--info",
        _ => "ds-status-dot ds-status-dot--neutral"
    };

    public static string StatusBadgeClass(string status) => status switch
    {
        ProjectStatus.Active => "text-bg-success",
        ProjectStatus.OnHold => "text-bg-warning",
        ProjectStatus.Planning => "text-bg-info",
        _ => "ds-badge-neutral"
    };
}
