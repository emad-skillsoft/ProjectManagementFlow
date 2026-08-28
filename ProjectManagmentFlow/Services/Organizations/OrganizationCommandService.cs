using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Organizations;

public class OrganizationCommandService : IOrganizationCommandService
{
    private const char Separator = '/';

    private readonly AppDbContext _context;
    private readonly IStringLocalizer<Messages> _text;

    public OrganizationCommandService(AppDbContext context, IStringLocalizer<Messages> text)
    {
        _context = context;
        _text = text;
    }

    public async Task<Organization> CreateAsync(
        string name, string? description, Guid? parentId, string type, string? code,
        Guid createdById,
        CancellationToken cancellationToken = default)
    {
        var trimmed = RequireName(name);
        var id = Guid.NewGuid();
        code = Blank(code);

        if (!OrgUnitTypes.IsKnown(type))
        {
            throw new ArgumentException(_text["Org_TypeUnknown"], nameof(type));
        }

        if (code is not null)
        {
            // الرمز فريدٌ داخل المنظّمة (جذرها) — لا في شجرةٍ أخرى.
            var rootId = parentId is null ? id : await _context.Organizations.AsNoTracking()
                .Where(o => o.Id == parentId.Value)
                .Select(o => o.RootId)
                .FirstOrDefaultAsync(cancellationToken);

            var duplicate = await _context.Organizations
                .AnyAsync(o => o.RootId == rootId && o.Code == code, cancellationToken);

            if (duplicate)
            {
                throw new ArgumentException(_text["Org_CodeTaken", code], nameof(code));
            }
        }

        var organization = new Organization
        {
            Id = id,
            Name = trimmed,
            Description = Blank(description),
            Type = type,
            Code = code,
            CreatedById = createdById
        };

        if (parentId is null)
        {
            // الجذر organization إلزاماً — فوقه لا يكون شيء في هذا الموديل.
            if (type != OrgUnitTypes.Organization)
            {
                throw new ArgumentException(_text["Org_RootMustBeOrganization"], nameof(type));
            }

            organization.ParentId = null;
            organization.RootId = id;
            organization.Path = Segment(id);
            organization.Depth = 0;
        }
        else
        {
            var parent = await LiveAsync(parentId.Value, cancellationToken)
                ?? throw new InvalidOperationException(_text["Org_ParentNotFound"]);

            if (parent.Depth + 1 > Organization.MaxDepth)
            {
                throw new InvalidOperationException(_text["Org_MaxDepth", Organization.MaxDepth]);
            }

            // القاعدة: رتبة الابن أعلى من رتبة الأب — قسمٌ تحت شعبةٍ لا معنى له.
            if (OrgUnitTypes.GetRank(type) <= OrgUnitTypes.GetRank(parent.Type))
            {
                throw new InvalidOperationException(_text[
                    "Org_TypeRank",
                    _text[$"OrgType_{type}"].Value,
                    _text[$"OrgType_{parent.Type}"].Value]);
            }

            organization.ParentId = parent.Id;
            organization.RootId = parent.RootId;
            organization.Path = parent.Path + Segment(id);
            organization.Depth = (short)(parent.Depth + 1);
        }

        _context.Organizations.Add(organization);
        await _context.SaveChangesAsync(cancellationToken);
        return organization;
    }

    public async Task<bool> UpdateAsync(
        Guid organizationId, string name, string? description,
        CancellationToken cancellationToken = default)
    {
        var organization = await LiveAsync(organizationId, cancellationToken);
        if (organization is null) return false;

        organization.Name = RequireName(name);
        organization.Description = Blank(description);
        organization.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var organization = await LiveAsync(organizationId, cancellationToken);
        if (organization is null) return false;

        // الرفض مقصود: حذف أمٍّ بضغطة واحدة يُخفي كلّ ما تحتها من مشاريع
        // دون أن يرى صاحب القرار حجم ما يفقده.
        var hasLiveChildren = await _context.Organizations
            .AnyAsync(o => o.ParentId == organizationId && o.DeletedAt == null, cancellationToken);

        if (hasLiveChildren)
        {
            throw new InvalidOperationException(_text["Org_HasChildren", organization.Name]);
        }

        organization.DeletedAt = DateTime.UtcNow;
        organization.UpdatedAt = organization.DeletedAt;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task MoveAsync(Guid organizationId, Guid? newParentId, CancellationToken cancellationToken = default)
    {
        var organization = await LiveAsync(organizationId, cancellationToken)
            ?? throw new InvalidOperationException(_text["Org_NotFound"]);

        if (organizationId == newParentId)
        {
            throw new InvalidOperationException(_text["Org_MoveIntoSelf"]);
        }

        if (newParentId is null)
        {
            // الجذر organization إلزاماً، وقاعدة الرتب تمنع أن يكون لغير الجذر
            // هذا النوع — فرفع وحدةٍ فرعيّة إلى جذرٍ لا سبيل إليه في هذا الموديل.
            throw new InvalidOperationException(_text["Org_MoveToRootNotAllowed",
                _text[$"OrgType_{organization.Type}"].Value]);
        }

        // قراءةٌ واحدة للأب الجديد تخدم الفحصين: الرتبة والشجرة.
        var newParent = await LiveAsync(newParentId.Value, cancellationToken)
            ?? throw new InvalidOperationException(_text["Org_ParentNotFound"]);

        if (OrgUnitTypes.GetRank(organization.Type) >= OrgUnitTypes.GetRank(newParent.Type))
        {
            throw new InvalidOperationException(_text[
                "Org_TypeRank",
                _text[$"OrgType_{organization.Type}"].Value,
                _text[$"OrgType_{newParent.Type}"].Value]);
        }

        // النقل داخل المنظّمة نفسها: نقلٌ بين شجرتين يعيد حساب RootId لذرّيّةٍ كاملة.
        if (organization.RootId != newParent.RootId)
        {
            throw new InvalidOperationException(_text["Org_MoveAcrossOrganizations"]);
        }

        var oldPath = organization.Path;
        var oldDepth = organization.Depth;

        string newPath;
        short newDepth;
        Guid newRootId;

        if (newParentId is null)
        {
            newPath = Segment(organization.Id);
            newDepth = 0;
            newRootId = organization.Id;
        }
        else
        {
            var parent = await LiveAsync(newParentId.Value, cancellationToken)
                ?? throw new InvalidOperationException(_text["Org_ParentNotFound"]);

            if (parent.Path.StartsWith(oldPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(_text["Org_MoveIntoDescendant"]);
            }

            newPath = parent.Path + Segment(organization.Id);
            newDepth = (short)(parent.Depth + 1);
            newRootId = parent.RootId;
        }

        if (newPath == oldPath) return;

        var subtree = await _context.Organizations
            .Where(o => o.Path.StartsWith(oldPath))
            .ToListAsync(cancellationToken);

        var deepest = subtree.Max(o => o.Depth) - oldDepth + newDepth;
        if (deepest > Organization.MaxDepth)
        {
            throw new InvalidOperationException(_text["Org_MaxDepth", Organization.MaxDepth]);
        }

        var movedAt = DateTime.UtcNow;

        foreach (var node in subtree)
        {
            node.Path = newPath + node.Path[oldPath.Length..];
            node.Depth = (short)(node.Depth - oldDepth + newDepth);
            node.RootId = newRootId;
            node.UpdatedAt = movedAt;
        }

        organization.ParentId = newParentId;

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// وجهات النقل المتاحة، كلّها في نطاق الفاعل. غير الصالحة تُعرض مع سببِها:
    /// الأب الحالي، الذات، فرْعٌ منها، رتبة غير مسموحة، أو عمقٌ يتجاوز الحدّ.
    /// </summary>
    public async Task<List<OrganizationMoveTarget>> GetMoveTargetsAsync(
        Guid organizationId, Guid actorId, bool isPlatformAdmin,
        CancellationToken cancellationToken = default)
    {
        var organization = await LiveAsync(organizationId, cancellationToken);
        if (organization is null) return [];

        // النطاق: جذور الشجرات التي يديرها الفاعل — كما في GetSwitchTargetsAsync.
        List<Guid> scopeRoots;
        if (isPlatformAdmin)
        {
            scopeRoots = await _context.Organizations.AsNoTracking()
                .Where(o => o.DeletedAt == null && o.ParentId == null)
                .Select(o => o.Id).Distinct().ToListAsync(cancellationToken);
        }
        else
        {
            scopeRoots = await _context.OrgMembers.AsNoTracking()
                .Where(m => m.UserId == actorId
                            && m.Status == OrgMemberStatus.Active
                            && (m.Role == OrgMemberRoles.Owner || m.Role == OrgMemberRoles.Admin)
                            && m.Organization!.DeletedAt == null)
                .Join(_context.Organizations.AsNoTracking(),
                    m => m.OrganizationId, o => o.Id, (m, o) => o.RootId)
                .Distinct().ToListAsync(cancellationToken);
        }

        var candidates = await _context.Organizations.AsNoTracking()
            .Where(o => o.DeletedAt == null
                        && (scopeRoots.Contains(o.RootId) || scopeRoots.Contains(o.Id)))
            .OrderBy(o => o.Path)
            .ToListAsync(cancellationToken);

        var orgPath = organization.Path;
        var orgDepth = organization.Depth;
        var orgTypeRank = OrgUnitTypes.GetRank(organization.Type);

        // أعماق ذرّيتها تُحسب مرة واحدة — نفس القيمة لكلّ وجهة.
        var maxDescendantDepth = await _context.Organizations.AsNoTracking()
            .Where(o => o.Path.StartsWith(orgPath))
            .Select(o => (short?)o.Depth)
            .MaxAsync(cancellationToken) ?? orgDepth;

        var targets = new List<OrganizationMoveTarget>();

        foreach (var cand in candidates)
        {
            if (cand.Id == organizationId)
            {
                targets.Add(new OrganizationMoveTarget(cand.Id, cand.Name, false,
                    "OrgMove_SameUnit", []));
                continue;
            }

            if (cand.Path.StartsWith(orgPath, StringComparison.Ordinal))
            {
                targets.Add(new OrganizationMoveTarget(cand.Id, cand.Name, false,
                    "OrgMove_IntoDescendant", []));
                continue;
            }

            if (cand.Id == organization.ParentId)
            {
                targets.Add(new OrganizationMoveTarget(cand.Id, cand.Name, false,
                    "OrgMove_CurrentParent", []));
                continue;
            }

            // رتبة الهدف يجب أن تكون أعلاه (رقمٌ أصغر) — فرعٌ لا يقبل أمّاً.
            if (orgTypeRank <= OrgUnitTypes.GetRank(cand.Type))
            {
                targets.Add(new OrganizationMoveTarget(cand.Id, cand.Name, false,
                    "OrgMove_TypeRank",
                    [_text[$"OrgType_{organization.Type}"].Value,
                     _text[$"OrgType_{cand.Type}"].Value]));
                continue;
            }

            // العمق الجديد (للذروة) يجب أن يبقى ضمن الحدّ.
            var deepestAfter = maxDescendantDepth - orgDepth + cand.Depth + 1;
            if (deepestAfter > Organization.MaxDepth)
            {
                targets.Add(new OrganizationMoveTarget(cand.Id, cand.Name, false,
                    "OrgMove_MaxDepth", [Organization.MaxDepth]));
                continue;
            }

            targets.Add(new OrganizationMoveTarget(cand.Id, cand.Name, true, null, []));
        }

        return targets;
    }

    /// <summary>
    /// مانع الحذف: تحتها وحدات أو مشاريع قائمة. لا شيء إن كان الحذف ممكناً.
    /// </summary>
    public async Task<string?> GetDeleteBlockerAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        var org = await _context.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.DeletedAt == null, cancellationToken);
        if (org is null) return _text["Org_NotFound"].Value;

        var unitsUnder = await _context.Organizations.AsNoTracking()
            .CountAsync(o => o.ParentId == organizationId && o.DeletedAt == null, cancellationToken);

        var projectsUnder = await _context.Projects.AsNoTracking()
            .CountAsync(p => p.Organization!.Path.StartsWith(org.Path) && p.DeletedAt == null, cancellationToken);

        if (unitsUnder > 0)
        {
            return _text["OrgDelete_HasUnits", unitsUnder].Value;
        }

        if (projectsUnder > 0)
        {
            return _text["OrgDelete_HasProjects", projectsUnder].Value;
        }

        return null;
    }

    private Task<Organization?> LiveAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Organizations.FirstOrDefaultAsync(o => o.Id == id && o.DeletedAt == null, cancellationToken);

    private static string Segment(Guid id) => id.ToString("N") + Separator;

    private string RequireName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            throw new ArgumentException(_text["Org_NameRequired"], nameof(name));
        }

        if (trimmed.Length > 200)
        {
            throw new ArgumentException(_text["Org_NameTooLong"], nameof(name));
        }

        return trimmed;
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
