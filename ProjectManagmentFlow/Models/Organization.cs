namespace ProjectManagmentFlow.Models;

/// <summary>
/// منظّمة في شجرة: لها أب واحد وأبناء كُثر. الحقول الأربعة الشجريّة
/// (ParentId · RootId · Path · Depth) مشتقّة، ولا تُكتب إلّا من
/// IOrganizationCommandService.
/// </summary>
public class Organization
{
    public const int PathLength = 300;
    public const int MaxDepth = 8;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// نوع الوحدة — واحدٌ من OrgUnitTypes، يُختار عند الإنشاء.
    /// رتبة الابن أكبر من رتبة الأب، والجذر organization إلزاماً.
    /// </summary>
    public string Type { get; set; } = OrgUnitTypes.Organization;

    /// <summary>رمز مختصر، فريدٌ داخل المنظّمة (جذر الشجرة). قابلٌ لأن يكون فارغاً.</summary>
    public string? Code { get; set; }

    public Guid? ParentId { get; set; }
    public Organization? Parent { get; set; }

    /// <summary>جذر الشجرة. يساوي Id في الجذر نفسه.</summary>
    public Guid RootId { get; set; }

    /// <summary>معرّفات الأجداد ثمّ المعرّف، مفصولةً بـ'/' ومنتهيةً به.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>بُعدها عن الجذر: صفر للجذر، واحد للمستوى الثاني.</summary>
    public short Depth { get; set; }

    public Guid? CreatedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<Organization> Children { get; set; } = new List<Organization>();
    public ICollection<OrgMember> Members { get; set; } = new List<OrgMember>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();

    public bool IsRoot => ParentId is null;
}
