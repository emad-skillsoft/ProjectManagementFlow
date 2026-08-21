namespace ProjectManagmentFlow.Models;

public class TeamMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }

    public Guid? UserId { get; set; }

    public string? Role { get; set; }

    public Guid? AddedById { get; set; }

    public DateTime? JoinedAt { get; set; }
}
