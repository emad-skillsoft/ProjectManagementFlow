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
        var cards = rows.Select(row => ToCard(row, today)).ToList();
        var permissions = BoardPermissions.FromMembers(members, actorId, ownerId);

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
            where task.Id == taskId && task.ProjectId != null
            select new
            {
                task.Id,
                ProjectId = task.ProjectId!.Value,
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
        var subtasks = (await CardRows(row.ProjectId, cancellationToken, taskId))
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

    public async Task<IReadOnlyList<TaskCard>> GetMyTasksAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var rows = await CardRows(null, cancellationToken, null, userId);
        var today = DateOnly.FromDateTime(DisplayTime.RiyadhNow());

        // الملغاة والمنجزة خارج «مهامي»: الشاشة قائمةُ عملٍ لا أرشيف.
        // والسقف يمنع مستخدماً بمئات المهامّ من سحبها كلّها في طلبٍ واحد.
        return rows.Select(row => ToCard(row, today))
            .Where(card => card.Status != TaskState.Done && card.Status != TaskState.Cancelled)
            .OrderBy(card => card.DueDate is null)
            .ThenBy(card => card.DueDate)
            .ThenBy(card => card.Position)
            .Take(200)
            .ToList();
    }

    private async Task<List<CardRow>> CardRows(
        Guid? projectId,
        CancellationToken cancellationToken,
        Guid? parentTaskId = null,
        Guid? assigneeId = null)
    {
        var query =
            from task in context.Tasks.AsNoTracking()
            join assignee in context.Users.AsNoTracking()
                on task.AssigneeId equals (Guid?)assignee.Id into assignees
            from assignee in assignees.DefaultIfEmpty()
            where task.ProjectId != null
                  && (projectId == null || task.ProjectId == projectId)
                  && task.ParentTaskId == parentTaskId
                  && (assigneeId == null || task.AssigneeId == assigneeId)
            orderby task.Position, task.CreatedAt, task.Id
            select new CardRow(
                task.Id,
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
