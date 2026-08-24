namespace ProjectManagmentFlow.Models;

/// <summary>
/// حالة المشروع — ثوابت نصّيّة لا تعداد، فالقيم تُخزَّن وتُقرأ من قاعدة
/// البيانات مباشرة، وقيد CHECK في الهجرة يحرسها (انظر AppDbContext).
/// </summary>
public static class ProjectStatus
{
    public const string Planning = "planning";
    public const string Active   = "active";
    public const string OnHold   = "on_hold";
    public const string Done     = "done";

    public static readonly string[] All = [Planning, Active, OnHold, Done];

    public static bool IsKnown(string? value) => value is not null && All.Contains(value);
}

public static class ProjectPriority
{
    public const string Low    = "low";
    public const string Normal = "normal";
    public const string High   = "high";
    public const string Urgent = "urgent";

    public static readonly string[] All = [Low, Normal, High, Urgent];

    public static bool IsKnown(string? value) => value is not null && All.Contains(value);
}
