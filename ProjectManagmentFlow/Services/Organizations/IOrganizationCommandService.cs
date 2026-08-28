using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Organizations;

public interface IOrganizationCommandService
{
    /// <summary>
    /// ينشئ وحدة. بلا parentId يكون جذراً (يجب أن يكون النوع organization)،
    /// وبأبٍّ يجب أن تكون رتبة النوع أعلاه من رتبة الأب. الرمز فريدٌ داخل
    /// المنظّمة إن وُجد.
    /// </summary>
    Task<Organization> CreateAsync(
        string name, string? description, Guid? parentId, string type, string? code,
        Guid createdById,
        CancellationToken cancellationToken = default);

    /// <summary>يعدّل الاسم والوصف. لا يمسّ موقعها في الشجرة ولا نوعها.</summary>
    Task<bool> UpdateAsync(
        Guid organizationId, string name, string? description,
        CancellationToken cancellationToken = default);

    /// <summary>حذف ناعم. يُرفض إن كانت لها ذرّيّة حيّة.</summary>
    Task<bool> DeleteAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>ينقلها تحت أبٍ آخر ويعيد حساب الشجرة الفرعيّة كلّها.</summary>
    Task MoveAsync(Guid organizationId, Guid? newParentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// كلّ وحدةٍ يمكن نقل الوحدة المطلوبة إليها، مع سببِ المنع لغير الصالحة.
    /// النطاق: ما يديره الفاعل — كما في OrgWorkspaceService.GetSwitchTargetsAsync.
    /// </summary>
    Task<List<OrganizationMoveTarget>> GetMoveTargetsAsync(
        Guid organizationId, Guid actorId, bool isPlatformAdmin,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// سبب منع حذف الوحدة إن وُجد: تحتها وحدات أو مشاريع قائمة. لا شيء إن أُمكن.
    /// </summary>
    Task<string?> GetDeleteBlockerAsync(
        Guid organizationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// وجهة نقلٍ واحدة: الوصف، والصالحية، وسبب المنع إن مُنعت —
/// فيُعرض سببها في القائمة ولا يُخفى. ReasonKey يُقرأ عبر IStringLocalizer
/// بحججِ ReasonArgs.
/// </summary>
public sealed record OrganizationMoveTarget(
    Guid? TargetId,
    string Name,
    bool IsAllowed,
    string? ReasonKey,
    object?[] ReasonArgs);
