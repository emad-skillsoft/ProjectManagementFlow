namespace ProjectManagmentFlow.Services.Permissions;

public interface IPermissionService
{
    bool HasPermission(string permissionName);
}
