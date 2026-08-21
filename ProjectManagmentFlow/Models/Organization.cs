namespace ProjectManagmentFlow.Models;

/// <summary>منظّمة — الجذر الذي تتبعه المشاريع والأعضاء. تدعم الحذف الناعم عبر DeletedAt.</summary>
public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? CreatedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    // العلاقات
    public ICollection<OrgMember> Members { get; set; } = new List<OrgMember>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
