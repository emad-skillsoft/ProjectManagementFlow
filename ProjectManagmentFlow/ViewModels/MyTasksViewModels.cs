using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Localization;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services.Tasks;

namespace ProjectManagmentFlow.ViewModels;

public sealed class MyTaskRowViewModel
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string PriorityLabel { get; init; } = string.Empty;
    public string PriorityClass { get; init; } = string.Empty;
    public string? DueLabel { get; init; }
    public bool IsOverdue { get; init; }
    public bool IsComplete { get; init; }
    public int Subtasks { get; init; }
    public string? Href { get; init; }
    public List<TaskOptionViewModel> Statuses { get; init; } = [];
}

public sealed class MyTaskGroupViewModel
{
    public string Title { get; init; } = string.Empty;
    public string? Href { get; init; }
    public bool IsPersonal { get; init; }
    public string CountLabel { get; init; } = string.Empty;
    public List<MyTaskRowViewModel> Rows { get; init; } = [];
}

public sealed class MyTasksFilterViewModel
{
    public string Label { get; init; } = string.Empty;
    public string Href { get; init; } = string.Empty;
    public bool IsCurrent { get; init; }
}

public sealed class NewTaskProjectViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public List<BoardPersonViewModel> Members { get; init; } = [];
}

/// <summary>ما تحتاجه نافذة «مهمة جديدة» — مستقلّاً عن الصفحة التي تفتحها.</summary>
public sealed class NewTaskFormViewModel
{
    public List<NewTaskProjectViewModel> Projects { get; init; } = [];
    public List<TaskOptionViewModel> Visibilities { get; init; } = [];
    public List<TaskOptionViewModel> Priorities { get; init; } = [];
    public string ActorName { get; init; } = string.Empty;
    public bool HasProjects => Projects.Count > 0;
}

public sealed class MyTasksViewModel
{
    public List<MyTaskGroupViewModel> Groups { get; init; } = [];
    public List<MyTasksFilterViewModel> Filters { get; init; } = [];
    public required NewTaskFormViewModel NewTask { get; init; }
    public string? Error { get; init; }
    public string? Saved { get; init; }
}

/// <summary>
/// تحويل مهامّ المستخدم إلى صفوف الشاشة. مكانه هنا لا في المتحكّم، لأنّ الترشيح
/// والعدّ متلازمان: العنوان يقول «س من ص»، فمن يرشّح هو من يعدّ.
/// </summary>
public static class MyTasksBuilder
{
    public const string FilterOpen = "open";
    public const string FilterOverdue = "overdue";

    public static MyTasksViewModel Build(
        IStringLocalizer text,
        MyTasksView tasks,
        IReadOnlyList<TeamTaskTarget> targets,
        string actorName,
        string? filter,
        string? error,
        string? saved) => new()
    {
        Groups = tasks.Groups.Select(group => Group(text, group, filter)).ToList(),
        Filters = Filters(text, filter),
        NewTask = new NewTaskFormViewModel
        {
            ActorName = actorName,
            Projects = targets.Select(target => new NewTaskProjectViewModel
            {
                Id = target.ProjectId,
                Name = target.ProjectName,
                Members = target.Members.Select(member => new BoardPersonViewModel
                {
                    Id = member.UserId,
                    Name = member.Name,
                    Initials = NameInitials.Of(member.Name)
                }).ToList()
            }).ToList(),
            Visibilities =
            [
                Option(TaskVisibility.Project, text["NewTask_VisibleToProject"], true),
                Option(TaskVisibility.Private, text["NewTask_VisiblePrivate"], false)
            ],
            // الأشدّ أوّلاً: في RTL يقع أوّل عنصرٍ أقصى اليمين، فتُقرأ السلّم نازلة.
            Priorities = ProjectPriority.All.Reverse().Select(priority => Option(
                priority,
                text[$"ProjectPriority_{priority}"],
                priority == ProjectPriority.Normal)).ToList()
        },
        Error = error,
        Saved = saved
    };

    private static MyTaskGroupViewModel Group(
        IStringLocalizer text,
        MyTaskGroup group,
        string? filter)
    {
        var kept = group.Cards.Where(card => Keep(card, filter)).ToList();

        return new MyTaskGroupViewModel
        {
            Title = group.IsPersonal ? text["MyTasks_PersonalGroup"] : group.Title,
            Href = group.ProjectId is { } id ? $"/projects/{id}/board" : null,
            IsPersonal = group.IsPersonal,
            CountLabel = text["MyTasks_GroupCount", kept.Count, group.Cards.Count],
            Rows = kept.Select(card => Row(text, card, group.ProjectId)).ToList()
        };
    }

    private static MyTaskRowViewModel Row(IStringLocalizer text, TaskCard card, Guid? projectId) => new()
    {
        Id = card.Id,
        Code = card.Code,
        Title = card.Title,
        PriorityLabel = text[$"ProjectPriority_{card.Priority}"],
        PriorityClass = $"ds-priority ds-priority--{card.Priority}",
        DueLabel = card.DueDate?.Local(),
        IsOverdue = card.IsOverdue,
        IsComplete = card.Status is TaskState.Done or TaskState.Cancelled,
        Subtasks = card.SubtaskTotal,
        Href = projectId is { } id ? $"/projects/{id}/tasks/{card.Id}" : $"/Tasks/{card.Id}",
        Statuses = TaskState.All
            // «ملغاة» عمودٌ إداريّ لا يُنقل إليه من هذه الشاشة.
            .Where(status => status != TaskState.Cancelled || card.Status == TaskState.Cancelled)
            .Select(status => Option(status, text[$"TaskState_{status}"], status == card.Status))
            .ToList()
    };

    /// <summary>
    /// الترشيح إمّا مدىً («مفتوحة»/«متأخرة») وإمّا حالةً بعينها. الاسم واحد
    /// لأنّهما متنافيان: لا معنى لـ«مفتوحة ومنجزة» معاً.
    /// </summary>
    private static bool Keep(TaskCard card, string? filter) => filter switch
    {
        FilterOpen => card.Status is not TaskState.Done and not TaskState.Cancelled,
        FilterOverdue => card.IsOverdue,
        { } status when TaskState.IsKnown(status) => card.Status == status,
        _ => true
    };

    private static List<MyTasksFilterViewModel> Filters(IStringLocalizer text, string? current)
    {
        var filters = new List<MyTasksFilterViewModel>
        {
            Filter(text["MyTasks_FilterAll"], "/Tasks", string.IsNullOrEmpty(current)),
            Filter(text["MyTasks_FilterOpen"], $"/Tasks?filter={FilterOpen}", current == FilterOpen),
            Filter(text["MyTasks_FilterOverdue"], $"/Tasks?filter={FilterOverdue}", current == FilterOverdue)
        };

        // «ملغاة» عمودٌ إداريّ لا يظهر في «مهامي»، فلا شريحة له.
        filters.AddRange(TaskState.All
            .Where(status => status != TaskState.Cancelled)
            .Select(status => Filter(
                text[$"TaskState_{status}"],
                $"/Tasks?filter={status}",
                current == status)));

        return filters;
    }

    private static MyTasksFilterViewModel Filter(string label, string href, bool current) =>
        new() { Label = label, Href = href, IsCurrent = current };

    private static TaskOptionViewModel Option(string value, string label, bool selected) =>
        new() { Value = value, Label = label, IsSelected = selected };
}
