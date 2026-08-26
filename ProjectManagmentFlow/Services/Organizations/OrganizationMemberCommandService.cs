using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Services.Organizations;

public class OrganizationMemberCommandService : IOrganizationMemberCommandService
{
    private readonly AppDbContext _context;
    private readonly IStringLocalizer<Messages> _text;

    public OrganizationMemberCommandService(AppDbContext context, IStringLocalizer<Messages> text)
    {
        _context = context;
        _text = text;
    }

    public async Task<OrgMember> InviteAsync(
        Guid organizationId, Guid userId, string role, Guid invitedById,
        CancellationToken cancellationToken = default)
    {
        RequireAssignableRole(role);

        var organizationExists = await _context.Organizations
            .AnyAsync(o => o.Id == organizationId && o.DeletedAt == null, cancellationToken);

        if (!organizationExists)
        {
            throw new InvalidOperationException(_text["Org_NotFound"]);
        }

        var existing = await FindAsync(organizationId, userId, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException(existing.Status == OrgMemberStatus.Active
                ? _text["OrgMember_AlreadyMember"]
                : _text["OrgMember_AlreadyInvited"]);
        }

        var invite = new OrgMember
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = role,
            Status = OrgMemberStatus.Pending,
            InvitedById = invitedById
        };

        _context.OrgMembers.Add(invite);
        await _context.SaveChangesAsync(cancellationToken);
        return invite;
    }

    public async Task<bool> AcceptInviteAsync(
        Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var invite = await FindAsync(organizationId, userId, cancellationToken);
        if (invite is null || invite.Status != OrgMemberStatus.Pending) return false;

        invite.Status = OrgMemberStatus.Active;
        invite.JoinedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DenyInviteAsync(
        Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var invite = await FindAsync(organizationId, userId, cancellationToken);
        if (invite is null || invite.Status != OrgMemberStatus.Pending) return false;

        _context.OrgMembers.Remove(invite);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SuspendAsync(
        Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await FindAsync(organizationId, userId, cancellationToken);
        if (member is null || member.Status == OrgMemberStatus.Suspended) return false;

        if (IsActiveOwner(member))
        {
            await RequireAnotherOwnerAsync(organizationId, userId, cancellationToken);
        }

        member.Status = OrgMemberStatus.Suspended;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RestoreAsync(
        Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await FindAsync(organizationId, userId, cancellationToken);
        if (member is null || member.Status != OrgMemberStatus.Suspended) return false;

        // الموقوف يعود فعّالاً لا معلّقاً: دعوته قُبلت يوم انضمّ.
        member.Status = OrgMemberStatus.Active;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveAsync(
        Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await FindAsync(organizationId, userId, cancellationToken);
        if (member is null) return false;

        if (IsActiveOwner(member))
        {
            await RequireAnotherOwnerAsync(organizationId, userId, cancellationToken);
        }

        _context.OrgMembers.Remove(member);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ChangeRoleAsync(
        Guid organizationId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        RequireAssignableRole(role);

        var member = await FindAsync(organizationId, userId, cancellationToken);
        if (member is null) return false;

        if (member.Role == role) return true;

       
        if (IsActiveOwner(member))
        {
            await RequireAnotherOwnerAsync(organizationId, userId, cancellationToken);
        }

        member.Role = role;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task TransferOwnershipAsync(
        Guid organizationId, Guid fromUserId, Guid toUserId, CancellationToken cancellationToken = default)
    {
        if (fromUserId == toUserId)
        {
            throw new InvalidOperationException(_text["OrgMember_TransferToSelf"]);
        }

        var current = await FindAsync(organizationId, fromUserId, cancellationToken);
        if (current is null || current.Role != OrgMemberRoles.Owner)
        {
            throw new InvalidOperationException(_text["OrgMember_NotOwner"]);
        }

        var target = await FindAsync(organizationId, toUserId, cancellationToken);
        if (target is null || target.Status != OrgMemberStatus.Active)
        {
            throw new InvalidOperationException(_text["OrgMember_TargetNotActive"]);
        }

        // الخطوتان في حفظٍ واحد: لا لحظة تملك فيها المنظّمة مالكَين ولا صفراً.
        target.Role = OrgMemberRoles.Owner;
        current.Role = OrgMemberRoles.Admin;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private Task<OrgMember?> FindAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        _context.OrgMembers.FirstOrDefaultAsync(
            m => m.OrganizationId == organizationId && m.UserId == userId, cancellationToken);

    private static bool IsActiveOwner(OrgMember member) =>
        member.Role == OrgMemberRoles.Owner && member.Status == OrgMemberStatus.Active;

    private async Task RequireAnotherOwnerAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var anotherOwner = await _context.OrgMembers.AnyAsync(
            m => m.OrganizationId == organizationId
              && m.UserId != userId
              && m.Role == OrgMemberRoles.Owner
              && m.Status == OrgMemberStatus.Active,
            cancellationToken);

        if (!anotherOwner)
        {
            throw new InvalidOperationException(_text["OrgMember_LastOwner"]);
        }
    }

    private void RequireAssignableRole(string role)
    {
        if (!OrgMemberRoles.IsKnown(role))
        {
            throw new ArgumentException(_text["OrgMember_UnknownRole", role], nameof(role));
        }
    }
}
