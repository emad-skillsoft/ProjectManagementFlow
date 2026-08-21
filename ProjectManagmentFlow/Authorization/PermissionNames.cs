namespace ProjectManagmentFlow.Authorization;

/// <summary>
/// أسماء الصلاحيات كثوابت، حتّى لا تتفرّق النصوص الحرفيّة بين المتحكّمات والزراعة.
/// كلّ اسم هنا يجب أن يقابله صفّ في جدول Permissions.
/// </summary>
public static class PermissionNames
{
    public const string RolesView = "roles:view";
    public const string RolesManage = "roles:manage";

    public const string UsersView = "users:view";
    public const string UsersManage = "users:manage";

    public const string OrganizationsView = "organizations:view";
    public const string ProjectsView      = "projects:view";
    public const string TasksView         = "tasks:view";
    public const string TeamsView         = "teams:view";
}
