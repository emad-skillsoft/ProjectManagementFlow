using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Organizations;

public interface IOrganizationCommandService
{
    /// <summary>ينشئ منظّمة. بلا parentId تكون جذراً.</summary>
    Task<Organization> CreateAsync(
        string name, string? description, Guid? parentId, Guid createdById,
        CancellationToken cancellationToken = default);

    /// <summary>يعدّل الاسم والوصف. لا يمسّ موقعها في الشجرة.</summary>
    Task<bool> UpdateAsync(
        Guid organizationId, string name, string? description,
        CancellationToken cancellationToken = default);

    /// <summary>حذف ناعم. يُرفض إن كانت لها ذرّيّة حيّة.</summary>
    Task<bool> DeleteAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>ينقلها تحت أبٍ آخر ويعيد حساب الشجرة الفرعيّة كلّها.</summary>
    Task MoveAsync(Guid organizationId, Guid? newParentId, CancellationToken cancellationToken = default);
}
