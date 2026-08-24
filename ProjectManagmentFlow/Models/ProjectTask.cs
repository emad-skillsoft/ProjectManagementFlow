namespace ProjectManagmentFlow.Models;

/// <summary>
/// مهمّة ضمن مشروع، تدعم التداخل عبر ParentTaskId.
/// سُمّيت ProjectTask لا Task تفادياً للالتباس مع System.Threading.Tasks.Task.
/// </summary>
public class ProjectTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>المهمّة الأمّ (مرجع ذاتيّ) — فارغة للمهامّ الجذرية.</summary>
    public Guid? ParentTaskId { get; set; }
    public ProjectTask? ParentTask { get; set; }
    public ICollection<ProjectTask> Subtasks { get; set; } = new List<ProjectTask>();

    public string Title { get; set; } = string.Empty;

    /// <summary>رمز المهمّة داخل مشروعها، مثل T-29؛ التسلسل مستقلّ لكلّ مشروع.</summary>
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = TaskState.Todo;
    public string Priority { get; set; } = ProjectPriority.Normal;
    public string? Visibility { get; set; }

    /// <summary>المكلَّف بالمهمّة (يقابل users.id).</summary>
    public Guid? AssigneeId { get; set; }

    /// <summary>مُنشئ المهمّة (يقابل users.id).</summary>
    public Guid? CreatedById { get; set; }

    public DateOnly? DueDate { get; set; }
    public decimal? EstimateHours { get; set; }
    public decimal? Position { get; set; }

    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
