using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Authorization;
using ProjectManagmentFlow.Filters;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services;
using ProjectManagmentFlow.Services.Tasks;
using ProjectManagmentFlow.ViewModels;

namespace ProjectManagmentFlow.Controllers;

/// <summary>
/// «مهامي»: ما أُسند إلى المستخدم عبر المشاريع، ودفترُه الشخصيّ.
/// الشاشة قارئة في الأغلب؛ وأوامرها الثلاثة تُفوَّض إلى خدمات المهامّ
/// كي تبقى الحراسة في موضعٍ واحد — لا نسخةً ثانيةً هنا.
/// </summary>
[RequirePermission(PermissionNames.TasksView)]
public sealed class TasksController(
    ITaskQueryService taskQueries,
    ITaskCommandService taskCommands,
    IStringLocalizer<Messages> text) : Controller
{
    private const string TeamKind = "project";

    [HttpGet("/Tasks")]
    public Task<IActionResult> Index(string? filter, CancellationToken cancellationToken = default) =>
        PageAsync(filter, cancellationToken);

    [HttpPost("/Tasks/create")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create(
        string kind,
        string title,
        string priority,
        DateOnly? dueDate,
        Guid? projectId,
        Guid? assigneeId,
        string? visibility,
        string? description,
        string? filter,
        CancellationToken cancellationToken = default) =>
        CommandAsync(filter, "Task_Created", cancellationToken, async actorId =>
        {
            if (kind != TeamKind)
            {
                await taskCommands.CreatePersonalAsync(
                    new PersonalTaskInput(title, priority, dueDate, description),
                    actorId,
                    cancellationToken);
                return;
            }

            if (projectId is not { } project)
            {
                throw new DomainException(text["NewTask_ProjectRequired"]);
            }

            // الصلاحيّة تُحسم في CreateAsync؛ لا يُكرَّر الفحص هنا.
            await taskCommands.CreateAsync(
                project,
                new TaskCreateInput(
                    title,
                    assigneeId,
                    TaskState.Todo,
                    priority,
                    dueDate,
                    description,
                    TaskVisibility.IsKnown(visibility) ? visibility! : TaskVisibility.Project),
                actorId,
                cancellationToken);
        });

    [HttpPost("/Tasks/status")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ChangeStatus(
        Guid taskId,
        string status,
        string? filter,
        CancellationToken cancellationToken = default) =>
        CommandAsync(filter, "Task_Saved", cancellationToken, actorId =>
            taskCommands.MoveAsync(taskId, status, null, actorId, cancellationToken));

    // ── درج المهمّة الشخصية ──────────────────────────────────────────────
    // مهمّة المشروع لها مساراتها تحت /projects؛ وهذه بلا مشروع فتحتاج مثيلاتها.
    // الحراسة كلّها في خدمة الأوامر: صاحب الدفتر وحده يصل إليه.

    [HttpGet("/Tasks/{taskId:guid}")]
    public async Task<IActionResult> Details(
        Guid taskId, bool panel = false, CancellationToken cancellationToken = default)
    {
        if (!TryGetActorId(out var actorId)) return Forbid();

        var task = await taskQueries.GetDetailAsync(taskId, cancellationToken);

        // مهمّة مشروعٍ وصلت إلى هذا المسار: مكانها تحت مشروعها لا هنا.
        if (task?.ProjectId is { } projectId)
        {
            return RedirectToAction("TaskDetails", "ProjectBoard", new { id = projectId, taskId });
        }

        // «غير موجودة» لا «ممنوع»: لا يُستدلّ على دفتر غيرك.
        if (task is null || task.CreatedById != actorId) return NotFound();

        var permissions = new BoardPermissions(true, actorId);
        var model = new TaskDetailsViewModel
        {
            Task = task,
            Permissions = permissions,
            Statuses = Statuses(task.Status),
            Priorities = Priorities(task.Priority),
            Members = [],
            MaySubtask = true,
            MayEdit = true,
            Error = TempData["TaskError"] as string,
            Saved = TempData["TaskSaved"] as string
        };

        if (panel) return PartialView("_TaskDrawer", model);

        ViewData["Title"] = task.Code;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("/Tasks/{taskId:guid}/status")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DrawerStatus(Guid taskId, string status, CancellationToken cancellationToken = default) =>
        DrawerCommandAsync(taskId, cancellationToken,
            actorId => taskCommands.MoveAsync(taskId, status, null, actorId, cancellationToken));

    [HttpPost("/Tasks/{taskId:guid}/priority")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DrawerPriority(Guid taskId, string priority, CancellationToken cancellationToken = default) =>
        DrawerCommandAsync(taskId, cancellationToken,
            actorId => taskCommands.SetPriorityAsync(taskId, priority, actorId, cancellationToken));

    [HttpPost("/Tasks/{taskId:guid}/update")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DrawerUpdate(
        Guid taskId, string title, string? description, DateOnly? dueDate,
        CancellationToken cancellationToken = default) =>
        DrawerCommandAsync(taskId, cancellationToken,
            actorId => taskCommands.UpdateAsync(taskId, title, description, dueDate, actorId, cancellationToken));

    [HttpPost("/Tasks/{taskId:guid}/subtasks")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DrawerAddSubtask(Guid taskId, string title, CancellationToken cancellationToken = default) =>
        DrawerCommandAsync(taskId, cancellationToken,
            actorId => taskCommands.AddSubtaskAsync(taskId, title, actorId, cancellationToken));

    [HttpPost("/Tasks/{taskId:guid}/subtasks/{subtaskId:guid}/toggle")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DrawerToggleSubtask(
        Guid taskId, Guid subtaskId, bool done, CancellationToken cancellationToken = default) =>
        DrawerCommandAsync(taskId, cancellationToken,
            actorId => taskCommands.MoveAsync(
                subtaskId, done ? TaskState.Done : TaskState.Todo, null, actorId, cancellationToken));

    [HttpPost("/Tasks/{taskId:guid}/cancel")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DrawerCancel(Guid taskId, CancellationToken cancellationToken = default) =>
        DrawerCommandAsync(taskId, cancellationToken,
            actorId => taskCommands.CancelAsync(taskId, actorId, cancellationToken));

    [HttpPost("/Tasks/{taskId:guid}/restore")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DrawerRestore(Guid taskId, CancellationToken cancellationToken = default) =>
        DrawerCommandAsync(taskId, cancellationToken,
            actorId => taskCommands.RestoreAsync(taskId, actorId, cancellationToken));

    private async Task<IActionResult> DrawerCommandAsync(
        Guid taskId,
        CancellationToken cancellationToken,
        Func<Guid, Task> command)
    {
        if (!TryGetActorId(out var actorId)) return Forbid();

        try
        {
            await command(actorId);
            TempData["TaskSaved"] = text["Task_Saved"].Value;
        }
        catch (DomainException exception)
        {
            TempData["TaskError"] = exception.Message;
        }

        return RedirectToAction(nameof(Details), new { taskId });
    }

    private List<TaskOptionViewModel> Statuses(string? selected) =>
        TaskState.All.Select(status => new TaskOptionViewModel
        {
            Value = status,
            Label = text[$"TaskState_{status}"],
            IsSelected = selected == status
        }).ToList();

    private List<TaskOptionViewModel> Priorities(string? selected) =>
        ProjectPriority.All.Select(priority => new TaskOptionViewModel
        {
            Value = priority,
            Label = text[$"ProjectPriority_{priority}"],
            IsSelected = selected == priority
        }).ToList();

    private async Task<IActionResult> CommandAsync(
        string? filter,
        string successKey,
        CancellationToken cancellationToken,
        Func<Guid, Task> command)
    {
        if (!TryGetActorId(out var actorId)) return Forbid();

        try
        {
            await command(actorId);
            TempData["MyTasksSaved"] = text[successKey].Value;
        }
        catch (DomainException exception)
        {
            TempData["MyTasksError"] = exception.Message;
        }

        return RedirectToAction(nameof(Index), new { filter });
    }

    private async Task<IActionResult> PageAsync(string? filter, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Forbid();

        var model = MyTasksBuilder.Build(
            text,
            await taskQueries.GetMyTasksAsync(actorId, cancellationToken),
            await taskQueries.GetTeamTaskTargetsAsync(actorId, cancellationToken),
            User.Identity?.Name ?? string.Empty,
            filter,
            TempData["MyTasksError"] as string,
            TempData["MyTasksSaved"] as string);

        ViewData["Title"] = text["Nav_MyTasks"].Value;
        return View(model);
    }

    private bool TryGetActorId(out Guid actorId) => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);
}
