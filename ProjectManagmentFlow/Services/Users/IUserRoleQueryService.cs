using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Users;

public interface IUserRoleQueryService
{
    Task<List<Role>> GetRolesByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
