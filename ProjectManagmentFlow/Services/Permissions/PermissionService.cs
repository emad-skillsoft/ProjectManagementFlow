using ProjectManagmentFlow.Services.Security;
using ProjectManagmentFlow.Services.Users;

namespace ProjectManagmentFlow.Services.Permissions;


public class PermissionService : IPermissionService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PermissionService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool HasPermission(string permissionName)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return false;

        return user.HasClaim(UserPrincipalFactory.PermissionClaimType, permissionName);
    }
}
