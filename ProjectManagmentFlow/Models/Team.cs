namespace ProjectManagmentFlow.Models;

public class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }

    public Guid? CreatedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // العلاقات
    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();

    /// <summary>
    /// فريق المشروع الواحد. القاعدة «لكلّ مشروعٍ فريقٌ واحد يحمل اسمه» تعيش هنا
    /// وحدها، فلا تُكتب مرّةً عند إنشاء المشروع وأخرى عند أوّل إضافة عضو.
    /// </summary>
    public static Team ForProject(Project project, string? name, Guid actorId) => new()
    {
        ProjectId = project.Id,
        Name = string.IsNullOrWhiteSpace(name) ? project.Name : name.Trim(),
        CreatedById = actorId,
        CreatedAt = DateTime.UtcNow
    };
}
