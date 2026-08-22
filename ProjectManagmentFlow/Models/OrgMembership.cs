namespace ProjectManagmentFlow.Models;

/// <summary>حالة العضويّة. الدعوة صفّ في OrgMembers بحالة Pending.</summary>
public static class OrgMemberStatus
{
    public const string Pending = "pending";
    public const string Active = "active";

    public static bool IsKnown(string? value) => value is Pending or Active;
}

/// <summary>دور العضو داخل منظّمته — غير أدوار المنصّة في جدول Roles.</summary>
public static class OrgMemberRoles
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Member = "member";

    public static bool IsKnown(string? value) => value is Owner or Admin or Member;
}
