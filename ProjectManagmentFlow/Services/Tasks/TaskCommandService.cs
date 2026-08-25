using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services.Activity;
using ProjectManagmentFlow.Services.Teams;

namespace ProjectManagmentFlow.Services.Tasks;

public sealed class TaskCommandService(
    AppDbContext context,
    IStringLocalizer<Messages> text,
    IActivityService activity,
    ITeamQueryService teams) : ITaskCommandService
{
    private const string CodePrefix = "T-";
    private const string PersonalPrefix = "P-";
    private const decimal PositionGap = 1000m;
    private const decimal MinimumGap = 0.000002m;

    public async Task<ProjectTask> CreateAsync(
        Guid projectId,
        TaskCreateInput input,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var project = await LiveProjectAsync(projectId, cancellationToken)
            ?? throw new DomainException(text["Project_NotFound"]);
        var permissions = await PermissionsAsync(projectId, actorId, cancellationToken);
        if (!permissions.CanCreate)
        {
            throw new DomainException(text["Task_CreateNotAllowed"]);
        }

        ValidateStatus(input.Status);
        ValidatePriority(input.Priority);
        ValidateVisibility(input.Visibility);
        if (input.AssigneeId is { } assigneeId)
        {
            await RequireTeamMemberAsync(projectId, assigneeId, cancellationToken);
        }

        var task = new ProjectTask
        {
            ProjectId = projectId,
            Title = RequireTitle(input.Title),
            Description = RequireDescription(input.Description),
            Status = input.Status,
            Priority = input.Priority,
            Visibility = input.Visibility,
            AssigneeId = input.AssigneeId,
            DueDate = input.DueDate,
            Position = await NextPositionAsync(projectId, null, input.Status, cancellationToken),
            CreatedById = actorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Tasks.Add(task);
        activity.Add(project.OrganizationId, projectId, actorId, ActivityEntities.Task, task.Id,
            ActivityActions.Created, new { Name = task.Title, task.Code });
        await SaveWithCodeAsync(task, cancellationToken);
        return task;
    }

    /// <summary>
    /// المهمّة الشخصية لا مشروع لها ولا فريق: تُسند إلى منشئها وتُختم «خاصّة».
    /// لا حارس صلاحيّةٍ هنا لأنّ كلّ مستخدمٍ يملك دفتره — والحارس أنّها لا تُرى لغيره.
    /// </summary>
    public async Task<ProjectTask> CreatePersonalAsync(
        PersonalTaskInput input,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ValidatePriority(input.Priority);

        var task = new ProjectTask
        {
            ProjectId = null,
            Title = RequireTitle(input.Title),
            Description = RequireDescription(input.Description),
            Status = TaskState.Todo,
            Priority = input.Priority,
            Visibility = TaskVisibility.Private,
            AssigneeId = actorId,
            DueDate = input.DueDate,
            Position = await NextPositionAsync(null, null, TaskState.Todo, cancellationToken),
            CreatedById = actorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Tasks.Add(task);
        activity.Add(null, null, actorId, ActivityEntities.Task, task.Id,
            ActivityActions.Created, new { Name = task.Title });
        await SaveWithCodeAsync(task, cancellationToken);
        return task;
    }

    public async Task<ProjectTask> AddSubtaskAsync(
        Guid parentTaskId,
        string title,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var parent = await LoadAsync(parentTaskId, cancellationToken);
        var permissions = await PermissionsForAsync(parent, actorId, cancellationToken);
        if (!permissions.CanSubtask(ToCard(parent)))
        {
            throw new DomainException(text["Task_SubtaskNotAllowed"]);
        }

        var organizationId = parent.ProjectId is { } parentProjectId
            ? (await LiveProjectAsync(parentProjectId, cancellationToken)
                ?? throw new DomainException(text["Project_NotFound"])).OrganizationId
            : null;
        var task = new ProjectTask
        {
            ProjectId = parent.ProjectId,
            ParentTaskId = parent.Id,
            Title = RequireTitle(title),
            Status = TaskState.Todo,
            Priority = parent.Priority,
            Visibility = parent.Visibility,
            AssigneeId = parent.AssigneeId,
            CreatedById = actorId,
            Position = await NextPositionAsync(parent.ProjectId, parent.Id, TaskState.Todo, cancellationToken),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Tasks.Add(task);
        activity.Add(organizationId, parent.ProjectId, actorId, ActivityEntities.Task, task.Id,
            ActivityActions.Added, new { Name = task.Title, Parent = parent.Title });
        await SaveWithCodeAsync(task, cancellationToken);
        return task;
    }

    public async Task UpdateAsync(
        Guid taskId,
        string title,
        string? description,
        DateOnly? dueDate,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var task = await LoadAsync(taskId, cancellationToken);

        // التعديل حقُّ من أنشأ المهمّة، ومن يدير اللوحة.
        var permissions = await PermissionsForAsync(task, actorId, cancellationToken);
        if (!permissions.CanEdit(ToCard(task)))
        {
            throw new DomainException(text["Task_EditNotAllowed"]);
        }

        task.Title = RequireTitle(title);
        task.Description = RequireDescription(description);
        task.DueDate = dueDate;
        task.UpdatedAt = DateTime.UtcNow;
        await AddAndSaveAsync(task, actorId, ActivityActions.Updated, new { Name = task.Title }, cancellationToken);
    }

    public async Task MoveAsync(
        Guid taskId,
        string status,
        Guid? afterTaskId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        await MoveCoreAsync(
            taskId,
            status,
            afterTaskId,
            actorId,
            ActivityActions.StatusChanged,
            cancellationToken);
    }

    private async Task MoveCoreAsync(
        Guid taskId,
        string status,
        Guid? afterTaskId,
        Guid actorId,
        string activityAction,
        CancellationToken cancellationToken)
    {
        if (!TaskState.IsKnown(status))
        {
            throw new DomainException(text["Task_UnknownStatus"], nameof(ProjectTask.Status));
        }

        var task = await LoadAsync(taskId, cancellationToken);
        var permissions = await PermissionsForAsync(task, actorId, cancellationToken);
        if (!permissions.CanMove(ToCard(task)))
        {
            throw new DomainException(text["Task_MoveNotAllowed"]);
        }
        if (!permissions.ManagesBoard && (status == TaskState.Cancelled || task.Status == TaskState.Cancelled))
        {
            throw new DomainException(text["Task_MoveNotAllowed"]);
        }

        if (afterTaskId == taskId)
        {
            throw new DomainException(text["Task_NotFound"]);
        }

        if (afterTaskId is { } anchorId)
        {
            var anchor = await context.Tasks.AsNoTracking().FirstOrDefaultAsync(candidate =>
                candidate.Id == anchorId
                && candidate.ProjectId == task.ProjectId
                && candidate.ParentTaskId == task.ParentTaskId
                && candidate.Status == status,
                cancellationToken);
            if (anchor is null)
            {
                throw new DomainException(text["Task_NotFound"]);
            }
        }

        task.Status = status;
        task.CompletedAt = status == TaskState.Done ? DateTime.UtcNow : null;
        task.UpdatedAt = DateTime.UtcNow;
        await PlaceAsync(task, afterTaskId, cancellationToken);
        await AddAndSaveAsync(task, actorId, activityAction,
            new { Name = task.Title, Value = status }, cancellationToken);
    }

    public async Task SetPriorityAsync(
        Guid taskId,
        string priority,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ValidatePriority(priority);
        var task = await LoadAsync(taskId, cancellationToken);
        await RequireManagerAsync(task, actorId, "Task_EditNotAllowed", cancellationToken);

        task.Priority = priority;
        task.UpdatedAt = DateTime.UtcNow;
        await AddAndSaveAsync(task, actorId, ActivityActions.Updated,
            new { Name = task.Title, Value = priority }, cancellationToken);
    }

    public async Task AssignToAsync(
        Guid taskId,
        Guid? assigneeId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var task = await LoadAsync(taskId, cancellationToken);

        // المهمّة الشخصية لا تُسنَد إلى غير صاحبها: لا فريق يستقبلها.
        var projectId = task.ProjectId
            ?? throw new DomainException(text["Task_PersonalNotAssignable"], nameof(ProjectTask.AssigneeId));
        await RequireManagerAsync(task, actorId, "Task_EditNotAllowed", cancellationToken);
        if (assigneeId is { } memberId)
        {
            await RequireTeamMemberAsync(projectId, memberId, cancellationToken);
        }

        task.AssigneeId = assigneeId;
        task.UpdatedAt = DateTime.UtcNow;

        // سجلّ النشاط يُقرأ لا يُفهرس: الاسم لا المعرّف.
        var assigneeName = assigneeId is null
            ? text["TaskDetail_Unassigned"].Value
            : await context.Users.AsNoTracking()
                .Where(user => user.Id == assigneeId.Value)
                .Select(user => user.FullName ?? user.Email ?? "—")
                .FirstOrDefaultAsync(cancellationToken) ?? "—";

        await AddAndSaveAsync(task, actorId, ActivityActions.Assigned,
            new { Name = task.Title, Value = assigneeName }, cancellationToken);
    }

    // الإلغاء والاستعادة نقلٌ إلى عمودٍ بعينه؛ و MoveCoreAsync تحرس الإدارة
    // بنفسها للعمود «ملغاة»، فلا يُعاد حساب الصلاحيّة هنا (كان يكلّف ٦ استعلامات).
    public Task CancelAsync(Guid taskId, Guid actorId, CancellationToken cancellationToken = default) =>
        MoveCoreAsync(taskId, TaskState.Cancelled, null, actorId,
            ActivityActions.StatusChanged, cancellationToken);

    public Task RestoreAsync(Guid taskId, Guid actorId, CancellationToken cancellationToken = default) =>
        MoveCoreAsync(taskId, TaskState.Todo, null, actorId,
            ActivityActions.Restored, cancellationToken);

    private async Task SaveWithCodeAsync(
        ProjectTask task,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            task.Code = await NextCodeAsync(task, cancellationToken);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException exception) when (SqlErrors.IsUniqueViolation(exception))
            {
                context.Entry(task).State = EntityState.Added;
            }
        }

        throw new DomainException(text["Task_CodeCollision"]);
    }

    private async Task AddAndSaveAsync(
        ProjectTask task,
        Guid actorId,
        string action,
        object payload,
        CancellationToken cancellationToken)
    {
        var projectId = task.ProjectId;
        var organizationId = projectId is { } id
            ? await context.Projects.AsNoTracking()
                .Where(project => project.Id == id)
                .Select(project => project.OrganizationId)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        activity.Add(organizationId, projectId, actorId, ActivityEntities.Task, task.Id, action, payload);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task PlaceAsync(
        ProjectTask task,
        Guid? afterTaskId,
        CancellationToken cancellationToken)
    {
        var projectId = task.ProjectId;
        var owner = task.CreatedById;
        var column = await context.Tasks
            .Where(candidate => (projectId == null
                                    ? candidate.ProjectId == null && candidate.CreatedById == owner
                                    : candidate.ProjectId == projectId)
                                && candidate.ParentTaskId == task.ParentTaskId
                                && candidate.Status == task.Status
                                && candidate.Id != task.Id)
            .OrderBy(candidate => candidate.Position)
            .ThenBy(candidate => candidate.CreatedAt)
            .ToListAsync(cancellationToken);

        decimal Lower() => afterTaskId is null
            ? 0m
            : column.First(candidate => candidate.Id == afterTaskId).Position ?? 0m;
        decimal Upper()
        {
            if (afterTaskId is null)
            {
                return column.Count == 0 ? PositionGap * 2 : column[0].Position ?? PositionGap * 2;
            }

            var index = column.FindIndex(candidate => candidate.Id == afterTaskId);
            var lower = column[index].Position ?? 0m;
            return index + 1 < column.Count
                ? column[index + 1].Position ?? lower + PositionGap * 2
                : lower + PositionGap * 2;
        }

        var lower = Lower();
        var upper = Upper();
        if (upper - lower < MinimumGap)
        {
            for (var index = 0; index < column.Count; index++)
            {
                column[index].Position = (index + 1) * PositionGap;
            }

            lower = Lower();
            upper = Upper();
        }

        task.Position = lower + ((upper - lower) / 2m);
    }

    private async Task<BoardPermissions> PermissionsAsync(
        Guid projectId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var ownerId = await context.Projects.AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => project.OwnerId)
            .FirstOrDefaultAsync(cancellationToken);

        return BoardPermissions.FromMembers(
            await teams.GetMembersAsync(projectId, cancellationToken),
            actorId,
            ownerId);
    }

    /// <summary>
    /// صلاحيّة المهمّة حيث هي: من فريق مشروعها إن تبعت مشروعاً، ومن ملكيّتها
    /// إن كانت شخصية. وغيرُ صاحب الشخصية يُردّ بـ«غير موجودة» لا بـ«ممنوع»،
    /// كي لا يُستدلّ على وجودها.
    /// </summary>
    private async Task<BoardPermissions> PermissionsForAsync(
        ProjectTask task,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        if (task.ProjectId is { } projectId)
        {
            return await PermissionsAsync(projectId, actorId, cancellationToken);
        }

        if (task.CreatedById != actorId) throw new DomainException(text["Task_NotFound"]);
        return new BoardPermissions(true, actorId);
    }

    private async Task RequireManagerAsync(
        ProjectTask task,
        Guid actorId,
        string errorKey,
        CancellationToken cancellationToken)
    {
        if (!(await PermissionsForAsync(task, actorId, cancellationToken)).ManagesBoard)
        {
            throw new DomainException(text[errorKey]);
        }
    }

    private async Task RequireTeamMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var member = (await teams.GetMembersAsync(projectId, cancellationToken))
            .Any(candidate => candidate.UserId == userId);
        if (!member)
        {
            throw new DomainException(text["Task_AssigneeNotTeamMember"], nameof(ProjectTask.AssigneeId));
        }
    }

    private async Task<ProjectTask> LoadAsync(Guid taskId, CancellationToken cancellationToken) =>
        await context.Tasks.FirstOrDefaultAsync(task => task.Id == taskId, cancellationToken)
        ?? throw new DomainException(text["Task_NotFound"]);

    private Task<Project?> LiveProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        context.Projects.FirstOrDefaultAsync(project => project.Id == projectId && project.DeletedAt == null, cancellationToken);

    /// <summary>
    /// أكبر رقمٍ في المشروع يُحسب في القاعدة لا بجلب رموزه كلّها:
    /// الدالّة تُنادى داخل حلقة إعادة المحاولة، فجلبُ الجدول ثلاث مرّات لا يُحتمل.
    /// </summary>
    private async Task<string> NextCodeAsync(ProjectTask task, CancellationToken cancellationToken)
    {
        // تسلسل المشروع T-n، وتسلسل الدفتر الشخصيّ P-n لكلّ مستخدمٍ على حدة.
        var prefix = task.ProjectId is null ? PersonalPrefix : CodePrefix;
        var owner = task.CreatedById;
        var projectId = task.ProjectId;

        var sequence = await context.Tasks.AsNoTracking()
            .Where(candidate => (projectId == null
                                    ? candidate.ProjectId == null && candidate.CreatedById == owner
                                    : candidate.ProjectId == projectId)
                                && candidate.Code.StartsWith(prefix))
            .Select(candidate => (int?)Convert.ToInt32(candidate.Code.Substring(prefix.Length)))
            .MaxAsync(cancellationToken) ?? 0;

        return prefix + (sequence + 1);
    }

    private async Task<decimal> NextPositionAsync(
        Guid? projectId,
        Guid? parentTaskId,
        string status,
        CancellationToken cancellationToken)
    {
        var max = await context.Tasks.AsNoTracking()
            .Where(task => (projectId == null ? task.ProjectId == null : task.ProjectId == projectId)
                           && task.ParentTaskId == parentTaskId
                           && task.Status == status)
            .Select(task => task.Position)
            .MaxAsync(cancellationToken);
        return (max ?? 0m) + PositionGap;
    }

    private void ValidatePriority(string priority)
    {
        if (!ProjectPriority.IsKnown(priority))
        {
            throw new DomainException(text["Task_UnknownPriority"], nameof(ProjectTask.Priority));
        }
    }

    private void ValidateVisibility(string visibility)
    {
        if (!TaskVisibility.IsKnown(visibility))
        {
            throw new DomainException(text["Task_UnknownVisibility"], nameof(ProjectTask.Visibility));
        }
    }

    private void ValidateStatus(string status)
    {
        if (!TaskState.IsKnown(status))
        {
            throw new DomainException(text["Task_UnknownStatus"], nameof(ProjectTask.Status));
        }
    }

    private string RequireTitle(string? title)
    {
        var value = (title ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            throw new DomainException(text["Task_TitleRequired"], nameof(ProjectTask.Title));
        }

        if (value.Length > 200)
        {
            throw new DomainException(text["Task_TitleTooLong"], nameof(ProjectTask.Title));
        }

        return value;
    }

    private string? RequireDescription(string? description)
    {
        if (description?.Length > 4000)
        {
            throw new DomainException(text["Task_DescriptionTooLong"], nameof(ProjectTask.Description));
        }

        return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private static TaskCard ToCard(ProjectTask task) => new(
        task.Id,
        task.ProjectId,
        TaskVisibility.Read(task.Visibility),
        task.Code,
        task.Title,
        task.Description,
        task.Status,
        task.Priority,
        task.AssigneeId,
        null,
        task.CreatedById,
        task.DueDate,
        false,
        0,
        0,
        task.Position ?? 0m);
}
