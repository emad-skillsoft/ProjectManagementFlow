using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services.Organizations;

namespace ProjectManagmentFlow.ViewModels;

public sealed class OrgSwitchTargetViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
    public string Meta { get; init; } = string.Empty;
    public string Href { get; init; } = string.Empty;
    public bool IsCurrent { get; init; }
}

public sealed class OrgTabViewModel
{
    public string Label { get; init; } = string.Empty;
    public string Href { get; init; } = string.Empty;
    public bool IsCurrent { get; init; }
}

/// <summary>ترويسة الصفحة: هويّة المنظّمة، ومبدّلها، وتبويباتها.</summary>
public sealed class OrgHeaderViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string SwitchHint { get; init; } = string.Empty;
    public List<OrgSwitchTargetViewModel> Targets { get; init; } = [];
    public List<OrgTabViewModel> Tabs { get; init; } = [];
    public bool CanSwitch => Targets.Count > 1;
}

public sealed class OrgStatViewModel
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string CssClass { get; init; } = string.Empty;
}

public sealed class OrgProjectRowViewModel
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Href { get; init; } = string.Empty;
    public string ProgressLabel { get; init; } = string.Empty;
    public int Percent { get; init; }
}

public sealed class OrgActivityRowViewModel
{
    public string Text { get; init; } = string.Empty;
    public string Time { get; init; } = string.Empty;
    public string Accent { get; init; } = string.Empty;
}

public sealed class OrgBoardViewModel
{
    public required OrgHeaderViewModel Header { get; init; }
    public List<OrgStatViewModel> Stats { get; init; } = [];
    public List<OrgProjectRowViewModel> Projects { get; init; } = [];
    public List<OrgActivityRowViewModel> Activity { get; init; } = [];
    public string ProjectsHref { get; init; } = string.Empty;
}

public sealed class OrgMemberRowViewModel
{
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string UnitName { get; init; } = string.Empty;
    public Guid UnitId { get; init; }
    public string RoleLabel { get; init; } = string.Empty;
    public string RoleClass { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusDot { get; init; } = string.Empty;
    public bool IsSuspended { get; init; }
    public bool MayAct { get; init; }
    public List<string> PlatformRoles { get; init; } = [];
}

public sealed class OrgMembersViewModel
{
    public required OrgHeaderViewModel Header { get; init; }
    public List<OrgMemberRowViewModel> Rows { get; init; } = [];
    public List<OrgInviteCandidate> Candidates { get; init; } = [];
    public List<TaskOptionViewModel> Roles { get; init; } = [];
    public string CountLabel { get; init; } = string.Empty;
    public string? Search { get; init; }
    public bool CanManage { get; init; }
    public bool CanGovern { get; init; }
    public bool CanEditPlatformRoles { get; init; }
    public string? Error { get; init; }
    public string? Saved { get; init; }
}

public sealed class OrgSettingsViewModel
{
    public required OrgHeaderViewModel Header { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? DeputyId { get; init; }
    public List<OrgMemberRowViewModel> DeputyChoices { get; init; } = [];
    public string CodeFormat { get; init; } = string.Empty;
    public bool CanGovern { get; init; }
    public string? Error { get; init; }
    public string? Saved { get; init; }
}

/// <summary>
/// بناء ترويسة المنظّمة. مكانها هنا لا في متحكّم، لأنّ التبويبات الثلاثة
/// تشترك فيها — وتعريفها ثلاث مرّات يعني أنّ إضافة تبويبٍ تُنسى في اثنين.
/// </summary>
public static class OrgHeaderBuilder
{
    public static OrgHeaderViewModel Build(
        IStringLocalizer text,
        Organization organization,
        int childCount,
        IReadOnlyList<OrgSwitchTarget> targets,
        string currentTab)
    {
        return new OrgHeaderViewModel
        {
            Id = organization.Id,
            Name = organization.Name,
            Initials = NameInitials.Of(organization.Name),
            Subtitle = organization.ParentId is null
                ? text["OrgView_RootSubtitle", childCount]
                : text["OrgView_ChildSubtitle", childCount],
            StatusLabel = text["OrgView_ActiveOrg"],
            SwitchHint = text["OrgView_SwitchHint"],
            Targets = targets.Select(target => new OrgSwitchTargetViewModel
            {
                Id = target.Id,
                Name = target.Name,
                Initials = NameInitials.Of(target.Name),
                Meta = target.IsRoot
                    ? text["OrgView_TargetRootMeta", target.Projects, target.Members]
                    : text["OrgView_TargetChildMeta", target.Projects, target.Members],
                Href = $"/Organization/{TabSegment(currentTab)}?unit={target.Id}",
                IsCurrent = target.IsCurrent
            }).ToList(),
            Tabs =
            [
                Tab("OrgView_TabBoard", $"/Organization?unit={organization.Id}", "board", currentTab),
                Tab("OrgView_TabMembers", $"/Organization/Members?unit={organization.Id}", "members", currentTab),
                Tab("OrgView_TabSettings", $"/Organization/Settings?unit={organization.Id}", "settings", currentTab)
            ]
        };

        OrgTabViewModel Tab(string key, string href, string tab, string current) => new()
        {
            Label = text[key],
            Href = href,
            IsCurrent = current == tab
        };
    }

    // المبدّل يبقيك في التبويب نفسه: من «الأعضاء» إلى أعضاء المنظّمة الأخرى.
    private static string TabSegment(string currentTab) => currentTab switch
    {
        "members" => "Members",
        "settings" => "Settings",
        _ => string.Empty
    };
}
