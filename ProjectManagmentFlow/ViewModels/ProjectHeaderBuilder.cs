using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services.Projects;
using ProjectManagmentFlow.Services.Teams;

namespace ProjectManagmentFlow.ViewModels;

/// <summary>
/// ترويسة المشروع وشريط تبويباته. مكانها هنا لا في متحكّم، لأنّ صفحات المشروع
/// موزّعة على متحكّمين — وتعريف التبويبات مرّتين يعني أنّ إضافة تبويبٍ تُنسى في أحدهما.
/// </summary>
public static class ProjectHeaderBuilder
{
    public static ProjectHeaderViewModel Build(
        IStringLocalizer text,
        ProjectDetailRecord project,
        IReadOnlyList<TeamMemberCard> members,
        string currentTab,
        bool mayEdit,
        string unitHref)
    {
        var noDate = text["ProjectDetail_NoDates"].Value;
        var lead = members.FirstOrDefault(member => member.Role == TeamMemberRoles.Leader);

        return new ProjectHeaderViewModel
        {
            Id = project.Id,
            Name = project.Name,
            Code = project.Code,
            UnitName = project.OrganizationName,
            UnitHref = unitHref,
            UnitType = new UnitTypeBadgeViewModel
            {
                Label = text[$"OrgType_{project.OrganizationType}"].Value,
                IsRoot = project.OrganizationDepth == 0 && project.OrganizationType == "organization"
            },
            Status = text[$"ProjectStatus_{project.Status}"],
            StatusClass = ProjectPresentation.StatusBadgeClass(project.Status),
            Priority = text[$"ProjectPriority_{project.Priority}"],
            PriorityClass = $"ds-priority ds-priority--{project.Priority}",
            OwnerName = project.OwnerName,
            LeadName = lead?.Name,
            StartLabel = project.StartDate is { } start ? start.Local() : noDate,
            DueLabel = project.DueDate is { } due ? due.Local() : noDate,
            TeamLabel = text["ProjectDetail_TeamCount", members.Count],
            MayEdit = mayEdit,
            Tabs =
            [
                Tab("ProjectDetail_TabOverview", $"/projects/{project.Id}", "overview", true),
                Tab("Board_Title", $"/projects/{project.Id}/board", "board", true),
                Tab("ProjectDetail_TabTeam", $"/projects/{project.Id}/team", "team", true),
                Tab("ProjectDetail_TabActivity", $"/projects/{project.Id}/activity", "activity", true),
                Tab("ProjectDetail_TabFiles", $"/projects/{project.Id}/files", "files", false)
            ]
        };

        ProjectTabViewModel Tab(string key, string href, string tab, bool available) => new()
        {
            Label = text[key],
            Href = href,
            IsCurrent = currentTab == tab,
            IsAvailable = available
        };
    }
}
