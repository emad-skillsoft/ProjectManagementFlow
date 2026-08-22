using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Organizations;

public interface IOrganizationMemberCommandService
{
    /// <summary>يدعو مستخدماً بحالة معلّقة. يُرفض إن كانت له عضويّة أو دعوة قائمة.</summary>
    Task<OrgMember> InviteAsync(
        Guid organizationId, Guid userId, string role, Guid invitedById,
        CancellationToken cancellationToken = default);

    /// <summary>المدعوّ يقبل دعوته هو.</summary>
    Task<bool> AcceptInviteAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>المدعوّ يرفض دعوته هو، فيُحذف الصفّ.</summary>
    Task<bool> DenyInviteAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>يُخرج عضواً. يُرفض على المالك الأخير.</summary>
    Task<bool> RemoveAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>يغيّر دور العضو داخل المنظّمة. لا يُستعمل لنقل الملكيّة.</summary>
    Task<bool> ChangeRoleAsync(
        Guid organizationId, Guid userId, string role, CancellationToken cancellationToken = default);

    /// <summary>ينقل الملكيّة: المالك الحاليّ يصير مديراً، والمستلِم مالكاً.</summary>
    Task TransferOwnershipAsync(
        Guid organizationId, Guid fromUserId, Guid toUserId, CancellationToken cancellationToken = default);
}
