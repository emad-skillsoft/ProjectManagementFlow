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
