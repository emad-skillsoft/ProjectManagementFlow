namespace ProjectManagmentFlow.Models;

public class ProjectUpdate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid? AuthorId { get; set; }

    public DateOnly? UpdateDate { get; set; }
    public string? DoneSummary { get; set; }
    public string? Challenges { get; set; }
    public string? SupportNeeded { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
