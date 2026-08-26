namespace ProjectManagmentFlow.Services.Organizations;

/// <summary>
/// قراءات صفحة المنظّمة الثلاث (اللوحة · الأعضاء · الإعدادات) في خدمةٍ واحدة.
/// جُمعت لأنّها تشترك في الشجرة الفرعيّة نفسها وفي حساب الصلاحيّة نفسه؛
/// وتفريقها على الخدمات القائمة كان يعني إعادة حسابهما في كلّ منها.
/// </summary>
public interface IOrgWorkspaceService
{
    /// <summary>
    /// صلاحيّة الفاعل في المنظّمة. الأدوار موروثة نزولاً: مالك الأمّ مالكٌ
    /// في كلّ تابعةٍ لها — وهو ما يجعل مبدّل الترويسة ذا معنى.
    /// </summary>
    Task<OrgAccess> GetAccessAsync(
        Guid organizationId, Guid actorId, bool isPlatformAdmin,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// هل يدير الفاعل منظّمةً ما؟ يُسأل عند بناء الشريط: عرض «المنظّمة» لمن
    /// لا يديرها يضع في شريطه رابطاً يُردّ عنه.
    /// </summary>
    Task<bool> ManagesAnyAsync(
        Guid actorId, bool isPlatformAdmin, CancellationToken cancellationToken = default);

    /// <summary>المنظّمات التي يديرها الفاعل: ما هو مالكٌ أو نائبٌ فيه، وذرّيّتها.</summary>
    Task<IReadOnlyList<OrgSwitchTarget>> GetSwitchTargetsAsync(
        Guid actorId, bool isPlatformAdmin, Guid currentId,
        CancellationToken cancellationToken = default);

    Task<OrgDashboard> GetDashboardAsync(
        Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>أعضاء المنظّمة وذرّيّتها، وعمود «الإدارة» يقول أين كلٌّ منهم.</summary>
    Task<IReadOnlyList<OrgMemberCard>> GetMemberCardsAsync(
        Guid organizationId, Guid actorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrgInviteCandidate>> GetInviteCandidatesAsync(
        Guid organizationId, CancellationToken cancellationToken = default);
}
