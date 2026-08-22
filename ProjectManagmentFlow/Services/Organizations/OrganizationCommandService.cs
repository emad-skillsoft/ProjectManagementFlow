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
        string name, string? description, Guid? parentId, Guid createdById,
        CancellationToken cancellationToken = default)
    {
        var trimmed = RequireName(name);
        var id = Guid.NewGuid();

        var organization = new Organization
        {
            Id = id,
            Name = trimmed,
            Description = Blank(description),
            CreatedById = createdById
        };

        if (parentId is null)
        {
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
