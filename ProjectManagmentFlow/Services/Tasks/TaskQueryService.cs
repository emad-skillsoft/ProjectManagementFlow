using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services.Teams;

namespace ProjectManagmentFlow.Services.Tasks;

public sealed class TaskQueryService(
    AppDbContext context,
    ITeamQueryService teams) : ITaskQueryService
{
    public async Task<TaskBoard> GetBoardAsync(
        Guid projectId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var members = await teams.GetMembersAsync(projectId, cancellationToken);
        var ownerId = await context.Projects.AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.OwnerId)
            .FirstOrDefaultAsync(cancellationToken);
        var rows = await CardRows(projectId, cancellationToken);
        var today = DateOnly.FromDateTime(DisplayTime.RiyadhNow());
        var permissions = BoardPermissions.FromMembers(members, actorId, ownerId);

        // المهمّة الخاصّة تُحجب هنا لا في العرض: ما لا يُرى لا يُرسَل.
        var cards = rows.Select(row => ToCard(row, today))
            .Where(permissions.CanSee)
            .ToList();

        // «ملغاة» عمودٌ إداريّ: يُحجب عن العضو صفّاً وبطاقاتٍ معاً،
        // فلا يراه ولا يرى ما فيه.
        var columns = TaskState.All
            .Where(status => status != TaskState.Cancelled || permissions.SeesCancelled)
            .Select(status => new BoardColumn(
                status,
                cards.Where(card => card.Status == status)
                    .OrderBy(card => card.Position)
                    .ToList()))
            .ToList();

        return new TaskBoard(columns, permissions) { Members = members };
    }

    public async Task<TaskDetail?> GetDetailAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var row = await (
            from task in context.Tasks.AsNoTracking()
            join assignee in context.Users.AsNoTracking()
                on task.AssigneeId equals (Guid?)assignee.Id into assignees
            from assignee in assignees.DefaultIfEmpty()
            join creator in context.Users.AsNoTracking()
                on task.CreatedById equals (Guid?)creator.Id into creators
            from creator in creators.DefaultIfEmpty()
            where task.Id == taskId
            select new
            {
                task.Id,
                task.ProjectId,
                task.Code,
                task.Title,
                task.Description,
                task.Status,
                task.Priority,
                task.AssigneeId,
                AssigneeName = assignee == null ? null : assignee.FullName ?? assignee.Email,
                task.CreatedById,
                task.DueDate,
                task.CreatedAt,
                CreatedByName = creator == null ? null : creator.FullName ?? creator.Email
            }).FirstOrDefaultAsync(cancellationToken);

        if (row is null) return null;

        var today = DateOnly.FromDateTime(DisplayTime.RiyadhNow());
        var subtasks = (await SubtaskRows(row.ProjectId, taskId, cancellationToken))
            .Select(item => ToCard(item, today))
            .OrderBy(item => item.Position)
            .ToList();
        var activity = await (
            from entry in context.ActivityLog.AsNoTracking()
            join actor in context.Users.AsNoTracking()
                on entry.ActorId equals (Guid?)actor.Id into actors
            from actor in actors.DefaultIfEmpty()
            where entry.EntityType == ActivityEntities.Task && entry.EntityId == taskId
            orderby entry.CreatedAt descending, entry.Id descending
            select new TaskActivityRecord(
                actor == null ? null : actor.FullName ?? actor.Email,
                entry.Action,
                entry.Payload,
                entry.CreatedAt))
            .ToListAsync(cancellationToken);

        return new TaskDetail(
            row.Id,
            row.ProjectId,
            row.Code,
            row.Title,
            row.Description,
            row.Status,
            row.Priority,
            row.AssigneeId,
            row.AssigneeName,
            row.CreatedById,
            row.DueDate,
            IsOverdue(row.DueDate, row.Status, today),
            row.CreatedAt,
            row.CreatedByName,
            subtasks,
            activity);
    }

    /// <summary>
    /// ما أُسند إلى المستخدم عبر المشاريع، ومهامّه الشخصية — مجموعةً لكلّ مشروع
    /// والشخصية أخيراً. المهامّ الفرعيّة مستثناة: ترث المسنَد إليه من أمّها
    /// فإدراجها يُكرّر الصفّ نفسه مرّتين.
    /// </summary>
    public async Task<MyTasksView> GetMyTasksAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DisplayTime.RiyadhNow());

        var assigned = await (
            from task in context.Tasks.AsNoTracking()
            join project in context.Projects.AsNoTracking()
                on task.ProjectId equals project.Id
            join assignee in context.Users.AsNoTracking()
                on task.AssigneeId equals (Guid?)assignee.Id into assignees
            from assignee in assignees.DefaultIfEmpty()
            where task.AssigneeId == userId
                  && task.ParentTaskId == null
                  && project.DeletedAt == null
            orderby project.Name, task.DueDate, task.Position
            select new
            {
                Project = project.Name,
                Row = new CardRow(
                    task.Id,
                    task.ProjectId,
                    task.Visibility,
                    task.Code,
                    task.Title,
                    task.Description,
                    task.Status,
                    task.Priority,
                    task.AssigneeId,
                    assignee == null ? null : assignee.FullName ?? assignee.Email,
                    task.CreatedById,
                    task.DueDate,
                    task.Subtasks.Count,
                    task.Subtasks.Count(subtask => subtask.CompletedAt != null),
                    task.Position ?? 0m)
            }).ToListAsync(cancellationToken);

        var personal = await (
            from task in context.Tasks.AsNoTracking()
            where task.ProjectId == null
                  && task.CreatedById == userId
                  && task.ParentTaskId == null
            orderby task.DueDate, task.Position
            select new CardRow(
                task.Id,
                task.ProjectId,
                task.Visibility,
                task.Code,
                task.Title,
                task.Description,
                task.Status,
                task.Priority,
                task.AssigneeId,
                null,
                task.CreatedById,
                task.DueDate,
                task.Subtasks.Count,
                task.Subtasks.Count(subtask => subtask.CompletedAt != null),
                task.Position ?? 0m)).ToListAsync(cancellationToken);

        var groups = assigned
            .GroupBy(item => item.Project)
            .Select(group => new MyTaskGroup(
                group.First().Row.ProjectId,
                group.Key,
                false,
                group.Select(item => ToCard(item.Row, today)).ToList()))
            .ToList();

        if (personal.Count > 0)
        {
            groups.Add(new MyTaskGroup(
                null,
                string.Empty,
                true,
                personal.Select(row => ToCard(row, today)).ToList()));
        }

        return new MyTasksView(groups);
    }

    /// <summary>
    /// المشاريع التي يجوز للفاعل إنشاء مهمّة فريقٍ فيها — أي ما يملكه أو يقود
    /// فريقه أو ينوب عنه. تُرشَّح بالصلاحية نفسها التي يحرسها أمر الإنشاء،
    /// فلا تعرض الواجهة خياراً يُرفض عند الإرسال.
    /// </summary>
    public async Task<IReadOnlyList<TeamTaskTarget>> GetTeamTaskTargetsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // المرشّحون: ما يملكه، وما هو عضوٌ في فريقه. الحسم بعدها بـ BoardPermissions.
        var candidates = await (
            from project in context.Projects.AsNoTracking()
            where project.DeletedAt == null
                  && (project.OwnerId == userId
                      || context.TeamMembers.Any(member =>
                          member.UserId == userId
                          && context.Teams.Any(team =>
                              team.Id == member.TeamId && team.ProjectId == project.Id)))
            orderby project.Name
            select new { project.Id, project.Name, project.OwnerId })
            .ToListAsync(cancellationToken);

        var targets = new List<TeamTaskTarget>();
        foreach (var candidate in candidates)
        {
            var members = await teams.GetMembersAsync(candidate.Id, cancellationToken);
            if (!BoardPermissions.FromMembers(members, userId, candidate.OwnerId).CanCreate) continue;

            targets.Add(new TeamTaskTarget(
                candidate.Id,
                candidate.Name,
                members.Select(member => new TeamTaskAssignee(member.UserId, member.Name)).ToList()));
        }

        return targets;
    }

    /// <summary>
    /// فرعيّات مهمّةٍ بعينها. مستقلّ عن CardRows لأنّ ذاك يشترط المشروع —
    /// وفرعيّات المهمّة الشخصية بلا مشروع.
    /// </summary>
    private async Task<List<CardRow>> SubtaskRows(
        Guid? projectId,
        Guid parentTaskId,
        CancellationToken cancellationToken) =>
        await (
            from task in context.Tasks.AsNoTracking()
            join assignee in context.Users.AsNoTracking()
                on task.AssigneeId equals (Guid?)assignee.Id into assignees
            from assignee in assignees.DefaultIfEmpty()
            where task.ParentTaskId == parentTaskId
                  && (projectId == null ? task.ProjectId == null : task.ProjectId == projectId)
            orderby task.Position, task.CreatedAt, task.Id
            select new CardRow(
                task.Id,
                task.ProjectId,
                task.Visibility,
                task.Code,
                task.Title,
                task.Description,
                task.Status,
                task.Priority,
                task.AssigneeId,
                assignee == null ? null : assignee.FullName ?? assignee.Email,
                task.CreatedById,
                task.DueDate,
                task.Subtasks.Count,
                task.Subtasks.Count(subtask => subtask.CompletedAt != null),
                task.Position ?? 0m)).ToListAsync(cancellationToken);

    private async Task<List<CardRow>> CardRows(
        Guid? projectId,
        CancellationToken cancellationToken,
        Guid? parentTaskId = null)
    {
        var query =
            from task in context.Tasks.AsNoTracking()
            join assignee in context.Users.AsNoTracking()
                on task.AssigneeId equals (Guid?)assignee.Id into assignees
            from assignee in assignees.DefaultIfEmpty()
            where task.ProjectId != null
                  && (projectId == null || task.ProjectId == projectId)
                  && task.ParentTaskId == parentTaskId
            orderby task.Position, task.CreatedAt, task.Id
            select new CardRow(
                    task.Id,
                    task.ProjectId,
                    task.Visibility,
                    task.Code,
                    task.Title,
                    task.Description,
                    task.Status,
                    task.Priority,
                    task.AssigneeId,
                    assignee == null ? null : assignee.FullName ?? assignee.Email,
                    task.CreatedById,
                    task.DueDate,
                    task.Subtasks.Count,
                    task.Subtasks.Count(subtask => subtask.CompletedAt != null),
                    task.Position ?? 0m);

        return await query.ToListAsync(cancellationToken);
    }

    private static TaskCard ToCard(CardRow row, DateOnly today) => new(
        row.Id,
        row.ProjectId,
        TaskVisibility.Read(row.Visibility),
        row.Code,
        row.Title,
        row.Description,
        row.Status,
        row.Priority,
        row.AssigneeId,
        row.AssigneeName,
        row.CreatedById,
        row.DueDate,
        IsOverdue(row.DueDate, row.Status, today),
        row.SubtaskTotal,
        row.SubtaskDone,
        row.Position);

    private static bool IsOverdue(DateOnly? due, string status, DateOnly today) =>
        due is { } date && date < today && status is not TaskState.Done and not TaskState.Cancelled;

    private sealed record CardRow(
        Guid Id,
        Guid? ProjectId,
        string? Visibility,
        string Code,
        string Title,
        string? Description,
        string Status,
        string Priority,
        Guid? AssigneeId,
        string? AssigneeName,
        Guid? CreatedById,
        DateOnly? DueDate,
        int SubtaskTotal,
        int SubtaskDone,
        decimal Position);
}
