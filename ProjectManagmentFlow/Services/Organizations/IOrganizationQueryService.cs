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
}
