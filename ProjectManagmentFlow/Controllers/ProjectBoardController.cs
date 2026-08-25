using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Authorization;
using ProjectManagmentFlow.Filters;
using ProjectManagmentFlow.Localization;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services;
using ProjectManagmentFlow.Services.Organizations;
using ProjectManagmentFlow.Services.Projects;
using ProjectManagmentFlow.Services.Tasks;
using ProjectManagmentFlow.Services.Teams;
using ProjectManagmentFlow.ViewModels;

namespace ProjectManagmentFlow.Controllers;

/// <summary>لوحة المهام ومسار التفاصيل؛ يبقي المتحكّم العام للمشاريع صغيراً.</summary>
[RequirePermission(PermissionNames.ProjectsView)]
public sealed class ProjectBoardController(
    IProjectQueryService projects,
    IOrganizationQueryService organizations,
    ITeamQueryService teams,
    ITaskQueryService taskQueries,
    ITaskCommandService taskCommands,
    IStringLocalizer<Messages> text) : Controller
{
    [HttpGet("/projects/{id:guid}/board")]
    public async Task<IActionResult> Board(Guid id, CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeAsync(id, cancellationToken);
        if (access.Denied) return access.Failure!;
        if (!TryGetActorId(out var actorId)) return Forbid();

        var detail = access.Project!;
        var board = await taskQueries.GetBoardAsync(id, actorId, cancellationToken);
        var model = new ProjectBoardViewModel
        {
            Header = BuildHeader(detail, board.Members, "board", board.Permissions.ManagesBoard),
            ProjectId = id,
            Permissions = board.Permissions,
            Columns = board.Columns.Select(column => new BoardColumnViewModel
            {
                Column = column,
                Label = text[$"TaskState_{column.Status}"],
                Accent = Accent(column.Status),
                Cards = column.Cards.Select(card => new TaskCardViewModel
                {
                    Card = card,
                    MayMove = board.Permissions.CanMove(card),
                    MaySubtask = board.Permissions.CanSubtask(card)
                }).ToList()
            }).ToList(),
            Statuses = Statuses().Where(status =>
                board.Permissions.SeesCancelled || status.Value != TaskState.Cancelled).ToList(),
            Priorities = Priorities(),
            Members = People(board.Members),
            TotalCards = board.Columns.Sum(column => column.Cards.Count),
            Error = TempData["BoardError"] as string
        };

        await SetProjectBreadcrumbAsync(detail, cancellationToken);
        ViewData["Title"] = text["Board_Title"].Value;
        return View(model);
    }

    [HttpPost("/projects/{id:guid}/board/tasks")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTask(
        Guid id,
        string title,
        Guid? assigneeId,
        string status,
        string priority,
        DateOnly? dueDate,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeAsync(id, cancellationToken);
        if (access.Denied) return access.Failure!;
        if (!TryGetActorId(out var actorId)) return Forbid();

        try
        {
            await taskCommands.CreateAsync(
                id,
                new TaskCreateInput(title, assigneeId, status, priority, dueDate, description),
                actorId,
                cancellationToken);
        }
        catch (DomainException exception)
        {
            TempData["BoardError"] = exception.Message;
        }

        return RedirectToAction(nameof(Board), new { id });
    }

    [HttpPost("/projects/{id:guid}/board/move")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MoveTask(
        Guid id,
        Guid taskId,
        string status,
        Guid? afterTaskId,
        CancellationToken cancellationToken = default) =>
        BoardCommandAsync(
            id,
            taskId,
            actorId => taskCommands.MoveAsync(taskId, status, afterTaskId, actorId, cancellationToken),
            cancellationToken);

    [HttpGet("/projects/{id:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> TaskDetails(
        Guid id, Guid taskId, bool panel = false, CancellationToken cancellationToken = default)
    {
        var access = await AuthorizeAsync(id, cancellationToken);
        if (access.Denied) return access.Failure!;
        if (!TryGetActorId(out var actorId)) return Forbid();

        var task = await taskQueries.GetDetailAsync(taskId, cancellationToken);
        if (task is null || task.ProjectId != id) return NotFound();

        var members = await teams.GetMembersAsync(id, cancellationToken);
        var permissions = BoardPermissions.FromMembers(members, actorId, access.Project!.OwnerId);
        var model = new TaskDetailsViewModel
        {
            Header = BuildHeader(access.Project!, members, "board", permissions.ManagesBoard),
            Task = task,
            Permissions = permissions,
            Statuses = Statuses(task.Status)
                .Where(status => permissions.SeesCancelled || status.Value != TaskState.Cancelled)
                .ToList(),
            Priorities = Priorities(task.Priority),
            Members = People(members),
            MaySubtask = permissions.CanSubtask(new TaskCard(
                task.Id, task.ProjectId, TaskVisibility.Project,
                task.Code, task.Title, task.Description, task.Status, task.Priority,
                task.AssigneeId, task.AssigneeName, task.CreatedById,
                task.DueDate, task.IsOverdue, 0, 0, 0m)),
            MayEdit = permissions.CanEdit(task),
            Error = TempData["TaskError"] as string,
            Saved = TempData["TaskSaved"] as string
        };

        // الدرج يطلب الجزئيّة وحدها؛ والصفحة الكاملة تبقى للفتح المباشر وحين لا JS.
        if (panel) return PartialView("_TaskDrawer", model);

        await SetProjectBreadcrumbAsync(access.Project!, cancellationToken);
        ViewData["Title"] = task.Code;
        return View(model);
    }

    [HttpPost("/projects/{id:guid}/tasks/{taskId:guid}/update")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateTask(
        Guid id,
        Guid taskId,
        string title,
        string? description,
        DateOnly? dueDate,
        CancellationToken cancellationToken = default) =>
        DetailCommandAsync(id, taskId,
            actorId => taskCommands.UpdateAsync(taskId, title, description, dueDate, actorId, cancellationToken),
            "Activity_Updated", cancellationToken);

    [HttpPost("/projects/{id:guid}/tasks/{taskId:guid}/status")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ChangeStatus(
        Guid id,
        Guid taskId,
        string status,
        CancellationToken cancellationToken = default) =>
        DetailCommandAsync(id, taskId,
            actorId => taskCommands.MoveAsync(taskId, status, null, actorId, cancellationToken),
            "Activity_StatusChanged", cancellationToken);

    [HttpPost("/projects/{id:guid}/tasks/{taskId:guid}/priority")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SetPriority(
        Guid id,
        Guid taskId,
        string priority,
        CancellationToken cancellationToken = default) =>
        DetailCommandAsync(id, taskId,
            actorId => taskCommands.SetPriorityAsync(taskId, priority, actorId, cancellationToken),
            "Activity_Updated", cancellationToken);

    [HttpPost("/projects/{id:guid}/tasks/{taskId:guid}/assign")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AssignTask(
        Guid id,
        Guid taskId,
        Guid? assigneeId,
        CancellationToken cancellationToken = default) =>
        DetailCommandAsync(id, taskId,
            actorId => taskCommands.AssignToAsync(taskId, assigneeId, actorId, cancellationToken),
            "Activity_Assigned", cancellationToken);

    [HttpPost("/projects/{id:guid}/tasks/{taskId:guid}/cancel")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CancelTask(Guid id, Guid taskId, CancellationToken cancellationToken = default) =>
        DetailCommandAsync(id, taskId,
            actorId => taskCommands.CancelAsync(taskId, actorId, cancellationToken),
            "Activity_StatusChanged", cancellationToken);

    [HttpPost("/projects/{id:guid}/tasks/{taskId:guid}/restore")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RestoreTask(Guid id, Guid taskId, CancellationToken cancellationToken = default) =>
        DetailCommandAsync(id, taskId,
            actorId => taskCommands.RestoreAsync(taskId, actorId, cancellationToken),
            "Activity_Restored", cancellationToken);

    // التأشير نقلٌ بين «منجزة» و«للتنفيذ»؛ يحرسه CanMove كأيّ نقلٍ آخر،
    // والمهمّة الفرعيّة ترث المسنَد إليه من أمّها فيملك صاحبها تأشيرها.
    [HttpPost("/projects/{id:guid}/tasks/{taskId:guid}/subtasks/{subtaskId:guid}/toggle")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ToggleSubtask(
        Guid id,
        Guid taskId,
        Guid subtaskId,
        bool done,
        CancellationToken cancellationToken = default) =>
        DetailCommandAsync(id, taskId,
            actorId => taskCommands.MoveAsync(
                subtaskId, done ? TaskState.Done : TaskState.Todo, null, actorId, cancellationToken),
            "Activity_StatusChanged", cancellationToken);

    [HttpPost("/projects/{id:guid}/tasks/{taskId:guid}/subtasks")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddSubtask(
        Guid id,
        Guid taskId,
        string title,
        CancellationToken cancellationToken = default) =>
        DetailCommandAsync(id, taskId,
            actorId => taskCommands.AddSubtaskAsync(taskId, title, actorId, cancellationToken),
            "Activity_Added", cancellationToken);

    private async Task<IActionResult> BoardCommandAsync(
        Guid projectId,
        Guid taskId,
        Func<Guid, Task> command,
        CancellationToken cancellationToken)
    {
        var access = await AuthorizeAsync(projectId, cancellationToken);
        if (access.Denied) return access.Failure!;
        if (!TryGetActorId(out var actorId)) return Forbid();

        var task = await taskQueries.GetDetailAsync(taskId, cancellationToken);
        if (task is null || task.ProjectId != projectId) return NotFound();

        try
        {
            await command(actorId);
        }
        catch (DomainException exception)
        {
            TempData["BoardError"] = exception.Message;
        }

        return RedirectToAction(nameof(Board), new { id = projectId });
    }

    private async Task<IActionResult> DetailCommandAsync(
        Guid projectId,
        Guid taskId,
        Func<Guid, Task> command,
        string successKey,
        CancellationToken cancellationToken)
    {
        var access = await AuthorizeAsync(projectId, cancellationToken);
        if (access.Denied) return access.Failure!;
        if (!TryGetActorId(out var actorId)) return Forbid();

        var task = await taskQueries.GetDetailAsync(taskId, cancellationToken);
        if (task is null || task.ProjectId != projectId) return NotFound();

        try
        {
            await command(actorId);
            TempData["TaskSaved"] = text["Task_Saved"].Value;
        }
        catch (DomainException exception)
        {
            TempData["TaskError"] = exception.Message;
        }

        return RedirectToAction(nameof(TaskDetails), new { id = projectId, taskId });
    }

    private async Task<ProjectAccess> AuthorizeAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetDetailAsync(projectId, cancellationToken);
        if (project is null) return new ProjectAccess(NotFound(), null);
        if (!TryGetActorId(out var actorId) || project.OrganizationId is null)
        {
            return new ProjectAccess(Forbid(), null);
        }

        var paths = await organizations.GetScopePathsByUserAsync(actorId, cancellationToken);
        return OrgScope.Contains(paths, project.OrganizationPath)
            ? new ProjectAccess(null, project)
            : new ProjectAccess(Forbid(), null);
    }

    private ProjectHeaderViewModel BuildHeader(
        ProjectDetailRecord project,
        IReadOnlyList<TeamMemberCard> members,
        string currentTab,
        bool mayEdit) =>
        ProjectHeaderBuilder.Build(
            text,
            project,
            members,
            currentTab,
            mayEdit,
            project.OrganizationId is { } unitId
                ? Url.Action("Index", "Projects", new { unit = unitId }) ?? "/Projects"
                : "/Projects");

    private List<TaskOptionViewModel> Statuses(string? selected = null) =>
        TaskState.All.Select(status => new TaskOptionViewModel
        {
            Value = status,
            Label = text[$"TaskState_{status}"],
            IsSelected = selected == status
        }).ToList();

    private List<TaskOptionViewModel> Priorities(string? selected = null) =>
        ProjectPriority.All.Select(priority => new TaskOptionViewModel
        {
            Value = priority,
            Label = text[$"ProjectPriority_{priority}"],
            IsSelected = selected == priority
        }).ToList();

    private static List<BoardPersonViewModel> People(IReadOnlyList<TeamMemberCard> members) =>
        members.Select(member => new BoardPersonViewModel
        {
            Id = member.UserId,
            Name = member.Name,
            Initials = NameInitials.Of(member.Name)
        }).ToList();

    private async Task SetProjectBreadcrumbAsync(
        ProjectDetailRecord project,
        CancellationToken cancellationToken)
    {
        var ancestors = project.OrganizationId is { } unitId
            ? await organizations.GetAncestorsAsync(unitId, cancellationToken)
            : [];

        ViewData["Breadcrumb"] = BreadcrumbBuilder.ForProject(
            text, ancestors, project.Name,
            unit => Url.Action("Index", "Projects", new { unit }));
    }

    private static string Accent(string status) => status switch
    {
        TaskState.InProgress => "var(--ds-info)",
        TaskState.InReview => "var(--ds-warning)",
        TaskState.Done => "var(--ds-green-700)",
        TaskState.Cancelled => "var(--ds-danger)",
        _ => "var(--ds-neutral-500)"
    };

    private bool TryGetActorId(out Guid actorId) => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);

    private readonly record struct ProjectAccess(IActionResult? Failure, ProjectDetailRecord? Project)
    {
        public bool Denied => Failure is not null;
    }
}
