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
}
