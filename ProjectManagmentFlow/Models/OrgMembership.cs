namespace ProjectManagmentFlow.Models;

/// <summary>حالة العضويّة. الدعوة صفّ في OrgMembers بحالة Pending.</summary>
public static class OrgMemberStatus
{
    public const string Pending = "pending";
    public const string Active = "active";

    /// <summary>
    /// موقوف: يبقى الصفّ وتاريخه، ولا يصل صاحبه إلى شيء. غير الإزالة
    /// لأنّه قرارٌ يُرجَع عنه — ولذلك وُجد الزرّان معاً في الجدول.
    /// </summary>
    public const string Suspended = "suspended";

    public static readonly string[] All = [Pending, Active, Suspended];

    public static bool IsKnown(string? value) => value is not null && All.Contains(value);
}

/// <summary>دور العضو داخل منظّمته — غير أدوار المنصّة في جدول Roles.</summary>
public static class OrgMemberRoles
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Member = "member";

    public static bool IsKnown(string? value) => value is Owner or Admin or Member;
}
