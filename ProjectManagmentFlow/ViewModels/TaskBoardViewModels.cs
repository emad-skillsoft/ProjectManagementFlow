using ProjectManagmentFlow.Services.Tasks;

namespace ProjectManagmentFlow.ViewModels;

public sealed class TaskOptionViewModel
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool IsSelected { get; init; }
}

public sealed class BoardPersonViewModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
}

public sealed class TaskCardViewModel
{
    public required TaskCard Card { get; init; }
    public bool MayMove { get; init; }
    public bool MaySubtask { get; init; }
}

public sealed class BoardColumnViewModel
{
    public required BoardColumn Column { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Accent { get; init; } = string.Empty;
    public List<TaskCardViewModel> Cards { get; init; } = [];
}

public sealed class ProjectBoardViewModel
{
    public required ProjectHeaderViewModel Header { get; init; }
    public Guid ProjectId { get; init; }
    public required BoardPermissions Permissions { get; init; }
    public List<BoardColumnViewModel> Columns { get; init; } = [];
    public List<TaskOptionViewModel> Statuses { get; init; } = [];
    public List<TaskOptionViewModel> Priorities { get; init; } = [];
    public List<BoardPersonViewModel> Members { get; init; } = [];
    public int TotalCards { get; init; }
    public string? Error { get; init; }
}

public sealed class TaskDetailsViewModel
{
    /// <summary>فارغة للمهمّة الشخصية: لا مشروع لها ترويسة تخصّه.</summary>
    public ProjectHeaderViewModel? Header { get; init; }
    public required TaskDetail Task { get; init; }
    public required BoardPermissions Permissions { get; init; }
    public List<TaskOptionViewModel> Statuses { get; init; } = [];
    public List<TaskOptionViewModel> Priorities { get; init; } = [];
    public List<BoardPersonViewModel> Members { get; init; } = [];
    public bool MaySubtask { get; init; }
    public bool MayEdit { get; init; }
    public string? Error { get; init; }
    public string? Saved { get; init; }
}
