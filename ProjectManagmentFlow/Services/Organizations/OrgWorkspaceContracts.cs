namespace ProjectManagmentFlow.Services.Organizations;

/// <summary>
/// ما يملكه الفاعل في منظّمةٍ بعينها. يُحسب مرّةً في المتحكّم ويُمرّر،
/// فلا يُعاد استعلام الشجرة عند كلّ سؤالٍ عن صلاحيّة.
/// </summary>
public sealed record OrgAccess(bool IsOwner, bool IsDeputy, bool IsPlatformAdmin)
{
    public static readonly OrgAccess None = new(false, false, false);

    /// <summary>القراءة والتحرير معاً: المالك ونائبه وأدمن المنصّة.</summary>
    public bool CanManage => IsOwner || IsDeputy || IsPlatformAdmin;

    /// <summary>نقل الملكيّة وتعيين النائب: المالك وأدمن المنصّة فقط.</summary>
    public bool CanGovern => IsOwner || IsPlatformAdmin;
}

/// <summary>منظّمة في مبدّل الترويسة، بعدّاديها.</summary>
public sealed record OrgSwitchTarget(
    Guid Id,
    string Name,
    short Depth,
    bool IsRoot,
    int Projects,
    int Members,
    bool IsCurrent);

public sealed record OrgMemberCard(
    Guid UserId,
    string Name,
    string Email,
    string Role,
    string Status,
    Guid OrganizationId,
    string OrganizationName,
    bool IsSelf);

public sealed record OrgProjectProgress(
    Guid Id,
    string Code,
    string Name,
    int DoneTasks,
    int TotalTasks);

public sealed record OrgActivityEntry(
    string EntityType,
    string Action,
    string? Payload,
    string? ActorName,
    DateTime CreatedAt);

/// <summary>
/// أرقام اللوحة محسوبةٌ على الشجرة الفرعيّة لا على المنظّمة وحدها: المالك
/// يرى ما تحته كلّه، وإلّا بدت منظّمةٌ أمٌّ فارغةً وكلّ عملها في تابعاتها.
/// </summary>
public sealed record OrgDashboard(
    int ActiveProjects,
    int Members,
    int OpenTasks,
    int OverdueTasks,
    IReadOnlyList<OrgProjectProgress> RecentProjects,
    IReadOnlyList<OrgActivityEntry> RecentActivity);

/// <summary>مرشَّحٌ للدعوة: مستخدمٌ قائم ليس عضواً في هذه المنظّمة ولا مدعوّاً إليها.</summary>
public sealed record OrgInviteCandidate(Guid UserId, string Name, string Email);
