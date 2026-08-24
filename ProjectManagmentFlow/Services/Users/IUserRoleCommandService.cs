namespace ProjectManagmentFlow.Services.Users;

/// <summary>حصيلة ضبط الأدوار: كم أُسند وكم سُحب، وهل فشل الإسناد.</summary>
public sealed record UserRolesChange(int Assigned, int Removed, bool Failed)
{
    public bool Changed => Assigned + Removed > 0;
}

public interface IUserRoleCommandService
{
    /// <summary>
    /// يجعل أدوار المستخدم مطابقةً للمجموعة المعطاة: يحسب الفرق ويطبّقه.
    /// حساب الفرق قاعدةٌ من قواعد المجال، فلا يُعاد في كلّ متحكّم يستدعيها.
    /// </summary>
    Task<UserRolesChange> SetRolesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken = default);

    Task<bool> AssignRolesToUserAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default);
    Task<bool> RemoveRolesFromUserAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default);
}
