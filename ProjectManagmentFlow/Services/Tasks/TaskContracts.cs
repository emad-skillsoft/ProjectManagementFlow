using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services.Teams;

namespace ProjectManagmentFlow.Services.Tasks;

public sealed record TaskCard(
    Guid Id,
    Guid? ProjectId,
    string Visibility,
    string Code,
    string Title,
    string? Description,
    string Status,
    string Priority,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid? CreatedById,
    DateOnly? DueDate,
    bool IsOverdue,
    int SubtaskTotal,
    int SubtaskDone,
    decimal Position);

public sealed record BoardColumn(string Status, IReadOnlyList<TaskCard> Cards);

public sealed record BoardPermissions(bool ManagesBoard, Guid ActorId)
{
    public bool CanMove(TaskCard card) => ManagesBoard || card.AssigneeId == ActorId;

    /// <summary>
    /// المهمّة «الخاصّة» داخل مشروع تظهر لمنشئها والمسنَد إليه ومن يدير اللوحة فقط.
    /// تُطبَّق في الخدمة لا في العرض: بطاقةٌ لا تُرى يجب ألّا تُرسَل أصلاً.
    /// </summary>
    public bool CanSee(TaskCard card) =>
        card.Visibility != TaskVisibility.Private
        || ManagesBoard
        || card.AssigneeId == ActorId
        || card.CreatedById == ActorId;
    public bool CanSubtask(TaskCard card) => ManagesBoard || card.AssigneeId == ActorId;
    public bool CanCreate => ManagesBoard;

    /// <summary>تعديل حقول المهمّة: لمن أنشأها ولمن يدير اللوحة.</summary>
    public bool CanEdit(TaskCard card) => ManagesBoard || card.CreatedById == ActorId;

    public bool CanEdit(TaskDetail task) => ManagesBoard || task.CreatedById == ActorId;

    /// <summary>«ملغاة» عمودٌ إداريّ: لا يراه العضو ولا يستطيع النقل إليه.</summary>
    public bool SeesCancelled => ManagesBoard;

    /// <summary>
    /// المالك يدير اللوحة سواءٌ أكان في الفريق أم لا — ولذلك تُقرأ ملكيّته من
    /// المشروع نفسه. الاعتماد على TeamMemberCard.IsProjectOwner وحده يحبس
    /// المالكَ خارج مشروعه إن لم يكن عضواً في فريقه.
    /// </summary>
    public static BoardPermissions FromMembers(
        IReadOnlyList<TeamMemberCard> members,
        Guid actorId,
        Guid? projectOwnerId)
    {
        if (projectOwnerId == actorId) return new BoardPermissions(true, actorId);

        var me = members.FirstOrDefault(member => member.UserId == actorId);
        return new BoardPermissions(
            me is not null && (me.IsProjectOwner
                || me.Role == TeamMemberRoles.Leader
                || me.Role == TeamMemberRoles.Deputy),
            actorId);
    }
}

public sealed record TaskBoard(
    IReadOnlyList<BoardColumn> Columns,
    BoardPermissions Permissions)
{
    // تُعاد القائمة التي استُخدمت لحساب الصلاحية كي لا تعيد الترويسة الاستعلامات الثلاثة.
    public IReadOnlyList<TeamMemberCard> Members { get; init; } = [];
}

public sealed record TaskActivityRecord(
    string? ActorName,
    string Action,
    string? Payload,
    DateTime CreatedAt);

public sealed record TaskDetail(
    Guid Id,
    /// <summary>فارغ للمهمّة الشخصية: لا مشروع تتبعه.</summary>
    Guid? ProjectId,
    string Code,
    string Title,
    string? Description,
    string Status,
    string Priority,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid? CreatedById,
    DateOnly? DueDate,
    bool IsOverdue,
    DateTime CreatedAt,
    string? CreatedByName,
    IReadOnlyList<TaskCard> Subtasks,
    IReadOnlyList<TaskActivityRecord> Activity);

public sealed record TaskCreateInput(
    string Title,
    Guid? AssigneeId,
    string Status,
    string Priority,
    DateOnly? DueDate,
    string? Description = null,
    string Visibility = TaskVisibility.Project);

/// <summary>مشروعٌ يملك الفاعل فيه صلاحيّة إنشاء مهمّة فريق، ومن يجوز إسنادها إليه.</summary>
public sealed record TeamTaskTarget(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<TeamTaskAssignee> Members);

public sealed record TeamTaskAssignee(Guid UserId, string Name);

/// <summary>مجموعةٌ في «مهامي»: مشروعٌ واحد، أو المهامّ الشخصية.</summary>
public sealed record MyTaskGroup(
    Guid? ProjectId,
    string Title,
    bool IsPersonal,
    IReadOnlyList<TaskCard> Cards);

/// <summary>
/// المجموعات كلّها بلا ترشيح. الترشيح في طبقة العرض عمداً: العنوان يذكر
/// «س من ص»، فترشيحها هنا يُفقد المقام.
/// </summary>
public sealed record MyTasksView(IReadOnlyList<MyTaskGroup> Groups);

public sealed record PersonalTaskInput(
    string Title,
    string Priority,
    DateOnly? DueDate,
    string? Description = null);
