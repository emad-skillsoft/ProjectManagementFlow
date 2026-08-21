namespace ProjectManagmentFlow.Models;

/// <summary>عضويّة مستخدم في منظّمة، بدورها وحالتها.</summary>
public class OrgMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid? UserId { get; set; }

    public string? Role { get; set; }
    public string? Status { get; set; }

    public Guid? InvitedById { get; set; }

    public DateTime? JoinedAt { get; set; }
}
