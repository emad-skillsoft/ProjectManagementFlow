using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Projects;

public enum ProjectScope
{
    Active,
    Archived
}

/// <summary>سجلّ بطاقة المشروع الخام — يحوّله المتحكّم إلى ViewModel معروض.</summary>
public sealed record ProjectCard(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string Status,
    string Priority,
    Guid? OrganizationId,
    string? OrganizationName,
    short OrganizationDepth,
    Guid? OwnerId,
    string? OwnerName,
    DateOnly? DueDate,
    DateTime? ArchivedAt,
    int TotalTasks,
    int DoneTasks);

public sealed record ProjectStats(int TotalTasks, int DoneTasks, int OverdueTasks, int TeamCount);

/// <summary>
/// بيانات المشروع الخام لصفحات التفاصيل؛ أسماء المستخدمين ناتجة من ضمّ يدوي.
/// يحمل مسار الوحدة وجذرها كي يكفي هذا السجلّ وحده للتفويض ولقائمة المرشّحين،
/// فلا تُعاد قراءة المنظّمة في كلّ صفحة.
/// </summary>
public sealed record ProjectDetailRecord(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string Status,
    string Priority,
    Guid? OrganizationId,
    string OrganizationName,
    short OrganizationDepth,
    string OrganizationPath,
    Guid OrganizationRootId,
    Guid? OwnerId,
    string OwnerName,
    string CreatedByName,
    DateOnly? StartDate,
    DateOnly? DueDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ArchivedAt);

/// <summary>عضو متاح لاختيار مالك المشروع أو أحد أعضاء فريقه.</summary>
public sealed record ProjectPerson(
    Guid Id,
    string Name,
    string OrganizationName,
    string OrganizationRole);

public sealed record ProjectTeamMember(Guid UserId, string Role);

public sealed record ProjectProvisionResult(Project Project, Team Team, int MemberCount);
