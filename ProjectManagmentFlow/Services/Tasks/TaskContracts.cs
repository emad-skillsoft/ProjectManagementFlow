using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services.Teams;

namespace ProjectManagmentFlow.Services.Tasks;

public sealed record TaskCard(
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
    bool IsOverdue,
    int SubtaskTotal,
    int SubtaskDone,
    decimal Position);

public sealed record BoardColumn(string Status, IReadOnlyList<TaskCard> Cards);

public sealed record BoardPermissions(bool ManagesBoard, Guid ActorId)
{
    public bool CanMove(TaskCard card) => ManagesBoard || card.AssigneeId == ActorId;
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
    Guid ProjectId,
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
    string? Description = null);
