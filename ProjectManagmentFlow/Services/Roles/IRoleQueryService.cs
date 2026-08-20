using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Roles;

/// <summary>ملخّص دور مع عدد صلاحياته وأعضائه، للعرض في القوائم دون تحميل العلاقات كاملةً.</summary>
public record RoleSummary(
    Guid Id, string Name, string? NameEn, string Description, string? DescriptionEn,
    bool IsSystem, int PermissionCount, int MemberCount);

public interface IRoleQueryService
{
    Task<List<RoleSummary>> GetSummariesAsync(CancellationToken cancellationToken = default);

    Task<Role?> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<List<Role>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<Permission>> GetPermissionsByRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<bool> HasPermissionAsync(Guid roleId, string permissionName, CancellationToken cancellationToken = default);
}
