namespace ProjectManagmentFlow.Models;

/// <summary>حالة المهمّة المخزّنة في قاعدة البيانات.</summary>
public static class TaskState
{
    public const string Todo = "todo";
    public const string InProgress = "in_progress";
    public const string InReview = "in_review";
    public const string Done = "done";
    public const string Cancelled = "cancelled";

    public static readonly string[] All = [Todo, InProgress, InReview, Done, Cancelled];

    public static bool IsKnown(string? value) => value is not null && All.Contains(value);
}

/// <summary>
/// مدى رؤية المهمّة. «خاصّة» يقصرها على منشئها والمسنَد إليه ومن يدير اللوحة،
/// فلا تظهر لبقيّة الفريق. المهمّة بلا مشروع خاصّة إلزاماً — يحرسه قيدٌ في القاعدة.
/// </summary>
public static class TaskVisibility
{
    public const string Project = "project";
    public const string Private = "private";

    public static readonly string[] All = [Project, Private];

    public static bool IsKnown(string? value) => value is not null && All.Contains(value);

    /// <summary>القيمة المخزّنة قبل هذه الميزة فارغة؛ تُقرأ «على مستوى المشروع».</summary>
    public static string Read(string? stored) => stored == Private ? Private : Project;
}
