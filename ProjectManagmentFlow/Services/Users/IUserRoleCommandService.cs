namespace ProjectManagmentFlow.Services.Users;

public interface IUserRoleCommandService
{
    Task<bool> AssignRolesToUserAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default);
    Task<bool> RemoveRolesFromUserAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken = default);
}
