namespace ProjectManagmentFlow.Models;

/// <summary>دور العضو داخل فريق المشروع.</summary>
public static class TeamMemberRoles
{
    public const string Leader = "lead";
    public const string Deputy = "deputy";
    public const string Member = "member";

    public static readonly string[] All = [Leader, Deputy, Member];

    public static bool IsKnown(string? value) => value is not null && All.Contains(value);
}
