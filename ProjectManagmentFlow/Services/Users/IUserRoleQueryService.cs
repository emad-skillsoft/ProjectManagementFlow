using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Users;

public interface IUserRoleQueryService
{
    Task<List<Role>> GetRolesByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// أدوار المنصّة لجماعةٍ من المستخدمين في رحلةٍ واحدة. جدول الأعضاء يعرضها
    /// لكلّ صفّ، وسؤالها مستخدماً مستخدماً يعني استعلاماً لكلّ سطر.
    /// </summary>
    Task<Dictionary<Guid, List<Role>>> GetRolesByUsersAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}
