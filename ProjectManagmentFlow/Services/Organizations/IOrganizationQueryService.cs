using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Organizations;

public interface IOrganizationQueryService
{
    Task<Organization?> GetByIdAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<List<Organization>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>المنظّمات التي يملك المستخدم فيها عضويّة فعّالة.</summary>
    Task<List<Organization>> GetOrganizationsByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>سلسلة الأجداد من الجذر إلى المنظّمة نفسها — مصدر مسار التنقّل.</summary>
    Task<List<Organization>> GetAncestorsAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>الأبناء المباشرون.</summary>
    Task<List<Organization>> GetChildrenAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>المنظّمة وكلّ ما تحتها، مرتّبةً بالعمق.</summary>
    Task<List<Organization>> GetSubtreeAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>جذور الشجرات — المنظّمات بلا أب.</summary>
    Task<List<Organization>> GetRootsAsync(CancellationToken cancellationToken = default);

    /// <summary>عدد الأبناء المباشرين، دون جلب صفوفهم.</summary>
    Task<int> CountChildrenAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>هل تقع المنظّمة تحت الجدّ المذكور؟ المنظّمة ليست سليلة نفسها.</summary>
    Task<bool> IsDescendantOfAsync(Guid organizationId, Guid ancestorId, CancellationToken cancellationToken = default);

    /// <summary>بادئة المسار التي تُقيَّد بها استعلامات الشجرة الفرعيّة في الخدمات الأخرى.</summary>
    Task<string?> GetScopePathAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// نطاقات المستخدم كلّها في رحلة واحدة، منزوعةَ التداخل:
    /// عضويّة في منظّمة وفي تابعةٍ لها تُختصر إلى الأعلى لأنّ مسارها يشملها.
    /// </summary>
    Task<List<string>> GetScopePathsByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// الشجرة كاملةً في عقدة واحدة — استعلامٌ واحد لا استعلامٌ لكلّ عقدة.
    /// كلُّ عقدةٍ تحمل عدّاد مشاريعها المباشرة.
    /// </summary>
    Task<OrganizationUnitNode?> GetTreeAsync(Guid rootId, CancellationToken cancellationToken = default);

    /// <summary>
    /// أرقام وحدةٍ واحدة: مشاريعها المباشرة، وما تحتها من مشاريع،
    /// والوحدات الأبناء، والأعضاء (مباشر + موروثٌ من الأجداد).
    /// </summary>
    Task<UnitStats> GetUnitStatsAsync(Guid unitId, CancellationToken cancellationToken = default);
}

/// <summary>عقدة في شجرة الوحدات: هويّتها الكاملة وذرّيّتها، بلا أسماء مستخدَمين.</summary>
public sealed class OrganizationUnitNode
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? Code { get; init; }
    public short Depth { get; init; }
    public Guid? ParentId { get; init; }
    /// <summary>مشاريع هذه الوحدة مباشرةً — لا شامل ما تحتها.</summary>
    public int DirectProjects { get; init; }
    public List<OrganizationUnitNode> Children { get; init; } = [];

    public bool HasChildren => Children.Count > 0;
}

/// <summary>عدّادات لوحة الوحدة، محسوبة على النطاقات الثلاثة المختلفة.</summary>
public sealed record UnitStats(
    int DirectProjects,
    int SubtreeProjects,
    int SubtreeUnits,
    int DirectMembers,
    int InheritedMembers);
