namespace ProjectManagmentFlow.Services.Security;

/// <summary>
/// تجديد ختم الأمان للمستخدمين المتأثّرين بتغيّر الأدوار أو الصلاحيات،
/// ليُبطل جلساتهم القائمة فوراً بدل انتظار انتهاء فترة إعادة التحقّق.
/// </summary>
public interface ISecurityStampService
{
    Task RefreshUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
    Task RefreshRoleMembersAsync(Guid roleId, CancellationToken cancellationToken = default);
}
