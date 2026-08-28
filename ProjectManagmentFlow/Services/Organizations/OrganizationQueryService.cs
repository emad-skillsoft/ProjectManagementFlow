using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Organizations;

public class OrganizationQueryService : IOrganizationQueryService
{
    private const char Separator = '/';

    private readonly AppDbContext _context;

    public OrganizationQueryService(AppDbContext context) => _context = context;

    public Task<Organization?> GetByIdAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        Live().FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

    public Task<List<Organization>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Live().OrderBy(o => o.Path).ToListAsync(cancellationToken);

    /// <summary>المنظّمات التي يملك المستخدم فيها عضويّة فعّالة.</summary>
    public Task<List<Organization>> GetOrganizationsByUserAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        Live()
            .Where(o => o.Members.Any(m => m.UserId == userId && m.Status == OrgMemberStatus.Active))
            .OrderBy(o => o.Path)
            .ToListAsync(cancellationToken);

    /// <summary>سلسلة الأجداد من الجذر إلى المنظّمة نفسها — مصدر مسار التنقّل.</summary>
    public async Task<List<Organization>> GetAncestorsAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        var path = await Live()
            .Where(o => o.Id == organizationId)
            .Select(o => o.Path)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(path)) return [];

        // المسار يحمل معرّفات السلسلة كاملةً، فاستعلام واحد يكفي مهما عمقت الشجرة.
        // نستثني الفواصل لأنّ المجدول القديم يمزج صيغة «N» و «D» داخل سطرٍ واحد.
        var ids = path
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static segment =>
            {
                var compact = new string(segment.Where(char.IsLetterOrDigit).ToArray());
                if (compact.Length != 32) return Guid.Empty;
                return Guid.ParseExact(compact, "N");
            })
            .Where(id => id != Guid.Empty)
            .ToList();

        return await Live()
            .Where(o => ids.Contains(o.Id))
            .OrderBy(o => o.Depth)
            .ToListAsync(cancellationToken);
    }

    /// <summary>الأبناء المباشرون.</summary>
    public Task<List<Organization>> GetChildrenAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        Live()
            .Where(o => o.ParentId == organizationId)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);

    /// <summary>المنظّمة وكلّ ما تحتها، مرتّبةً بالعمق.</summary>
    public async Task<List<Organization>> GetSubtreeAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        var scope = await GetScopePathAsync(organizationId, cancellationToken);
        if (scope is null) return [];

        return await Live()
            .Where(o => o.Path.StartsWith(scope))
            .OrderBy(o => o.Depth)
            .ThenBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>جذور الشجرات — المنظّمات بلا أب.</summary>
    public Task<List<Organization>> GetRootsAsync(CancellationToken cancellationToken = default) =>
        Live()
            .Where(o => o.ParentId == null)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);

    /// <summary>عدد الأبناء المباشرين، دون جلب صفوفهم.</summary>
    public Task<int> CountChildrenAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        Live().CountAsync(o => o.ParentId == organizationId, cancellationToken);

    /// <summary>هل يقع المنظّمة تحت الجدّ المذكور؟ المنظّمة ليست سليلة نفسها.</summary>
    public async Task<bool> IsDescendantOfAsync(
        Guid organizationId, Guid ancestorId, CancellationToken cancellationToken = default)
    {
        if (organizationId == ancestorId) return false;

        var paths = await Live()
            .Where(o => o.Id == organizationId || o.Id == ancestorId)
            .Select(o => new { o.Id, o.Path })
            .ToListAsync(cancellationToken);

        var self = paths.FirstOrDefault(p => p.Id == organizationId)?.Path;
        var ancestor = paths.FirstOrDefault(p => p.Id == ancestorId)?.Path;

        return self is not null
            && ancestor is not null
            && self.StartsWith(ancestor, StringComparison.Ordinal);
    }

    /// <summary>بادئة المسار التي تُقيَّد بها استعلامات الشجرة الفرعيّة.</summary>
    public Task<string?> GetScopePathAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        Live()
            .Where(o => o.Id == organizationId)
            .Select(o => o.Path)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// نطاقات المستخدم كاملة، منزوعةَ التداخل: العضوية في منظّمة وفي تابعةٍ لها
    /// تُختصر إلى الأعلى لأنّ مسارها يشملها.
    /// </summary>
    public async Task<List<string>> GetScopePathsByUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var paths = await Live()
            .Where(o => o.Members.Any(m => m.UserId == userId && m.Status == OrgMemberStatus.Active))
            .Select(o => o.Path)
            .ToListAsync(cancellationToken);

        return OrgScope.Outermost(paths);
    }

    /// <summary>
    /// الشجرة كاملة في عقدة واحدة — استعلامٌ واحد لا استعلامٌ لكلّ عقدة:
    /// السطور كلّها تُجلب مرّةً واحدة، وبعدها يُبنى التفرّع في الذاكرة.
    /// </summary>
    /// <summary>
    /// الشجرة من الوحدة المذكورة نزولاً — لا من جذر المنظّمة. مالك إدارةٍ تابعة
    /// يرى ما يديره فقط؛ البناء من الجذر يكشف له أسماء الفروع الشقيقة ورموزها.
    /// </summary>
    public async Task<OrganizationUnitNode?> GetTreeAsync(
        Guid scopeUnitId, CancellationToken cancellationToken = default)
    {
        var scopePath = await Live()
            .Where(o => o.Id == scopeUnitId)
            .Select(o => o.Path)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(scopePath)) return null;

        var rows = await Live()
            .Where(o => o.Path.StartsWith(scopePath))
            .OrderBy(o => o.Path)
            .Select(o => new UnitTreeRow(
                o.Id, o.Name, o.Type, o.Code, o.ParentId, o.Depth))
            .ToListAsync(cancellationToken);

        var root = rows.FirstOrDefault(r => r.Id == scopeUnitId);
        if (root is null) return null;

        // جذر العرض بلا أبٍ ظاهر: أبوه خارج النطاق فلا يُربط به.
        root = root with { ParentId = null };
        rows = [.. rows.Select(r => r.Id == scopeUnitId ? root : r)];

        // مشاريع كلّ وحدة — استعلامٌ واحد مُجمَّع، لا استعلامٌ لكلّ عقدة.
        var projectCounts = await _context.Projects.AsNoTracking()
            .Where(p => p.Organization!.Path.StartsWith(scopePath) && p.DeletedAt == null)
            .GroupBy(p => p.OrganizationId!.Value)
            .Select(g => new { Unit = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Unit, x => x.Count, cancellationToken);

        rows = [.. rows.Select(r => r with { DirectProjects = projectCounts.GetValueOrDefault(r.Id) })];
        root = rows.First(r => r.Id == scopeUnitId);
        var byParent = new Dictionary<Guid, List<UnitTreeRow>>(
            rows.Where(r => r.ParentId is not null)
                .GroupBy(r => r.ParentId!.Value)
                .ToDictionary(group => group.Key, group => group.ToList()));

        return Build(root, byParent);

        static OrganizationUnitNode Build(
            UnitTreeRow row, IReadOnlyDictionary<Guid, List<UnitTreeRow>> childrenByParent)
        {
            childrenByParent.TryGetValue(row.Id, out var children);
            return new OrganizationUnitNode
            {
                Id = row.Id,
                Name = row.Name,
                Type = row.Type,
                Code = row.Code,
                Depth = row.Depth,
                ParentId = row.ParentId,
                DirectProjects = row.DirectProjects,
                Children = [.. (children ?? []).Select(c => Build(c, childrenByParent))]
            };
        }
    }

    /// <summary>
    /// أرقام لوحة الوحدة: مشاريعها المباشرة، ومشاريعها وما تحتها،
    /// والوحدات تحتها، والأعضاء (مباشر + موروَّث من الأجداد).
    /// </summary>
    public async Task<UnitStats> GetUnitStatsAsync(
        Guid unitId, CancellationToken cancellationToken = default)
    {
        var unit = await Live()
            .Where(o => o.Id == unitId)
            .Select(o => new { o.RootId, o.Path })
            .FirstOrDefaultAsync(cancellationToken);
        if (unit is null) return new UnitStats(0, 0, 0, 0, 0);

        // الأجداد: كلّ وحدةٍ في الشجرة نفسها أقصر مساراً من هذه.
        var ancestorIds = await Live()
            .Where(o => o.RootId == unit.RootId && o.Path.Length < unit.Path.Length)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        var directProjects = await _context.Projects.AsNoTracking()
            .CountAsync(p => p.OrganizationId == unitId && p.DeletedAt == null, cancellationToken);

        var subtreeProjects = await _context.Projects.AsNoTracking()
            .Where(p => p.DeletedAt == null
                        && p.Organization!.RootId == unit.RootId
                        && p.Organization.Path.StartsWith(unit.Path))
            .CountAsync(cancellationToken);

        var subtreeUnits = await Live()
            .Where(o => o.RootId == unit.RootId && o.Path.StartsWith(unit.Path) && o.Id != unitId)
            .CountAsync(cancellationToken);

        var directMembers = await _context.OrgMembers.AsNoTracking()
            .CountAsync(m => m.OrganizationId == unitId && m.Status == OrgMemberStatus.Active, cancellationToken);

        // الموروَّث: نشطٌ في أحد الأجداد، وليس له عضويةٌ فعلية في الوحدة نفسها.
        var inheritedMembers = ancestorIds.Count == 0
            ? 0
            : await (
                from member in _context.OrgMembers.AsNoTracking()
                where ancestorIds.Contains(member.OrganizationId!.Value)
                    && member.Status == OrgMemberStatus.Active
                    && !_context.OrgMembers.Any(direct =>
                        direct.OrganizationId == unitId
                        && direct.UserId == member.UserId
                        && direct.Status == OrgMemberStatus.Active)
                select member.UserId)
                .Distinct()
                .CountAsync(cancellationToken);

        return new UnitStats(
            directProjects, subtreeProjects, subtreeUnits, directMembers, inheritedMembers);
    }

    private IQueryable<Organization> Live() =>
        _context.Organizations.AsNoTracking().Where(o => o.DeletedAt == null);
}

/// <summary>
/// صفٌ خام من GetTreeAsync — يُبنى عليه عقدة الواجهة.
/// DirectProjects يُحسب في الاستعلام نفسه.
/// </summary>
internal sealed record UnitTreeRow(
    Guid Id,
    string Name,
    string Type,
    string? Code,
    Guid? ParentId,
    short Depth,
    int DirectProjects = 0);
