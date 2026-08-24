using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Organizations;

public interface IOrganizationMemberQueryService
{
    /// <summary>الأعضاء الفعليّون — دون الدعوات المعلّقة.</summary>
    Task<List<OrgMember>> GetMembersByOrgAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>دور المستخدم في المنظّمة، أو لا شيء إن لم يكن عضواً فعليّاً.</summary>
    Task<string?> GetMemberRoleAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>عدد الأعضاء الفعليّين، دون جلب صفوفهم.</summary>
    Task<int> CountMembersAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>الدعوات المعلّقة الصادرة من هذه المنظّمة — لصاحبها.</summary>
    Task<List<OrgMember>> GetPendingInvitesByOrgAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>الدعوات المعلّقة الواردة إلى المستخدم — مدخل القبول والرفض.</summary>
    Task<List<OrgMember>> GetInvitesByUserAsync(Guid userId, CancellationToken cancellationToken = default);

}
