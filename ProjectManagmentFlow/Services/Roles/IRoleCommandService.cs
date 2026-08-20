using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Roles;

public interface IRoleCommandService
{
    Task<Role> CreateAsync(
        string name, string description, string? nameEn, string? descriptionEn,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        Guid roleId, string name, string description, string? nameEn, string? descriptionEn,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<bool> AssignPermissionsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken = default);
    Task<bool> RevokePermissionsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken = default);
}
