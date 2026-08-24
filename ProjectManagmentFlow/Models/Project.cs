namespace ProjectManagmentFlow.Models;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }

    public Guid? OwnerId { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }

    public Guid? CreatedById { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    // العلاقات
    public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
    public ICollection<Team> Teams { get; set; } = new List<Team>();
    public ICollection<ProjectUpdate> Updates { get; set; } = new List<ProjectUpdate>();
    public ICollection<ActivityLog> Activities { get; set; } = new List<ActivityLog>();
}
