using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Authorization;
using ProjectManagmentFlow.Filters;
using ProjectManagmentFlow.Localization;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services;
using ProjectManagmentFlow.Services.Organizations;
using ProjectManagmentFlow.Services.Permissions;
using ProjectManagmentFlow.Services.Roles;
using ProjectManagmentFlow.Services.Users;
using ProjectManagmentFlow.ViewModels;

namespace ProjectManagmentFlow.Controllers;

/// <summary>
/// مساحة عمل المنظّمة بتبويباتها الثلاثة. يصل إليها مالك المنظّمة ونائبه
/// وأدمن المنصّة؛ والمالك يتنقّل بين منظّمته وذرّيّتها لأنّ الأدوار موروثة نزولاً.
/// </summary>
[RequirePermission(PermissionNames.OrganizationsView)]
public sealed class OrganizationController(
    IOrganizationQueryService organizations,
    IOrganizationCommandService organizationCommands,
    IOrganizationMemberCommandService memberCommands,
    IOrgWorkspaceService workspace,
    IUserRoleQueryService userRoles,
    IUserRoleCommandService userRoleCommands,
    IUserQueryService users,
    IRoleQueryService roles,
    IPermissionService permissions,
    IStringLocalizer<Messages> text) : Controller
{
    [HttpGet("/Organization")]
    public Task<IActionResult> Index(Guid? unit, CancellationToken cancellationToken = default) =>
        TabAsync(unit, "board", cancellationToken, async (organization, access, header) =>
        {
            var dashboard = await workspace.GetDashboardAsync(organization.Id, cancellationToken);
            return View("Board", new OrgBoardViewModel
            {
                Header = header,
                ProjectsHref = $"/Projects?unit={organization.Id}",
                Stats =
                [
                    Stat(dashboard.ActiveProjects, "OrgView_StatProjects", "ds-stat-card--primary"),
                    Stat(dashboard.Members, "OrgView_StatMembers", string.Empty),
                    Stat(dashboard.OpenTasks, "OrgView_StatOpenTasks", string.Empty),
                    Stat(dashboard.OverdueTasks, "OrgView_StatOverdue", "ds-stat-card--danger")
                ],
                Projects = dashboard.RecentProjects.Select(project => new OrgProjectRowViewModel
                {
                    Code = project.Code,
                    Name = project.Name,
                    Href = $"/projects/{project.Id}",
                    ProgressLabel = $"{project.DoneTasks}/{project.TotalTasks}",
                    Percent = project.TotalTasks == 0
                        ? 0
                        : project.DoneTasks * 100 / project.TotalTasks
                }).ToList(),
                Activity = dashboard.RecentActivity.Select(Activity).ToList()
            });
        });

    [HttpGet("/Organization/Members")]
    public Task<IActionResult> Members(
        Guid? unit, string? search, CancellationToken cancellationToken = default) =>
        TabAsync(unit, "members", cancellationToken, async (organization, access, header) =>
            View("Members", await MembersModelAsync(
                organization.Id, access, header, search, cancellationToken)));

    [HttpGet("/Organization/Settings")]
    public Task<IActionResult> Settings(Guid? unit, CancellationToken cancellationToken = default) =>
        TabAsync(unit, "settings", cancellationToken, async (organization, access, header) =>
        {
            var members = await workspace.GetMemberCardsAsync(
                organization.Id, ActorId(), cancellationToken);

            return View("Settings", new OrgSettingsViewModel
            {
                Header = header,
                Name = organization.Name,
                Description = organization.Description,
                DeputyId = members
                    .FirstOrDefault(member => member.Role == OrgMemberRoles.Admin
                                              && member.OrganizationId == organization.Id)?.UserId,
                DeputyChoices = members
                    .Where(member => member.OrganizationId == organization.Id
                                     && member.Role != OrgMemberRoles.Owner
                                     && member.Status == OrgMemberStatus.Active)
                    .Select(member => Row(member, access, []))
                    .ToList(),
                // صيغة الرمز نمطٌ لا عبارة: السنة ثمّ تسلسل، ولا تُترجَم.
                CodeFormat = $"{DisplayTime.RiyadhNow().Year}-###",
                CanGovern = access.CanGovern,
                Error = TempData["OrgError"] as string,
                Saved = TempData["OrgSaved"] as string
            });
        });

    [HttpPost("/Organization/Settings")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveSettings(
        Guid unit, string name, string? description, Guid? deputyId,
        CancellationToken cancellationToken = default) =>
        CommandAsync(unit, nameof(Settings), cancellationToken, async (organization, access) =>
        {
            if (!access.CanGovern) throw new DomainException(text["OrgView_NotAllowed"]);

            await organizationCommands.UpdateAsync(unit, name, description, cancellationToken);
            await SetDeputyAsync(unit, deputyId, cancellationToken);
        });

    [HttpPost("/Organization/Members/invite")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Invite(
        Guid unit, Guid userId, string role, CancellationToken cancellationToken = default) =>
        MemberCommandAsync(unit, cancellationToken, (access, actorId) =>
            memberCommands.InviteAsync(unit, userId, Assignable(role), actorId, cancellationToken));

    [HttpPost("/Organization/Members/role")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ChangeRole(
        Guid unit, Guid userId, Guid memberUnit, string role,
        CancellationToken cancellationToken = default) =>
        MemberCommandAsync(unit, cancellationToken, (access, actorId) =>
            memberCommands.ChangeRoleAsync(memberUnit, userId, Assignable(role), cancellationToken));

    [HttpPost("/Organization/Members/suspend")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Suspend(
        Guid unit, Guid userId, Guid memberUnit, CancellationToken cancellationToken = default) =>
        MemberCommandAsync(unit, cancellationToken, (access, actorId) =>
            memberCommands.SuspendAsync(memberUnit, userId, cancellationToken));

    [HttpPost("/Organization/Members/restore")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Restore(
        Guid unit, Guid userId, Guid memberUnit, CancellationToken cancellationToken = default) =>
        MemberCommandAsync(unit, cancellationToken, (access, actorId) =>
            memberCommands.RestoreAsync(memberUnit, userId, cancellationToken));

    [HttpPost("/Organization/Members/remove")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Remove(
        Guid unit, Guid userId, Guid memberUnit, CancellationToken cancellationToken = default) =>
        MemberCommandAsync(unit, cancellationToken, (access, actorId) =>
            memberCommands.RemoveAsync(memberUnit, userId, cancellationToken));

    // ── أدوار المنصّة لعضو ────────────────────────────────────────────────
    // انتقلت من صفحة «المستخدمون» إلى هنا: مكان إدارة الأشخاص صار جدول الأعضاء.

    [HttpGet("/Organization/Members/{userId:guid}/roles")]
    [RequirePermission(PermissionNames.UsersEdit)]
    public async Task<IActionResult> MemberRoles(
        Guid unit, Guid userId, CancellationToken cancellationToken = default)
    {
        var model = await MemberRolesModelAsync(unit, userId, cancellationToken);
        if (model is null) return NotFound();

        ViewData["Title"] = text["UserRoles_Title", model.DisplayName].Value;
        return View("MemberRoles", model);
    }

    [HttpPost("/Organization/Members/{userId:guid}/roles")]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionNames.UsersEdit)]
    public async Task<IActionResult> MemberRoles(
        Guid unit, Guid userId, UserRolesViewModel viewModel,
        CancellationToken cancellationToken = default)
    {
        if (await users.GetByIdAsync(userId, cancellationToken) is null) return NotFound();

        // لا يرفع المرء صلاحيّة نفسه: الحارس هنا وفي الخدمة معاً.
        if (userId == ActorId())
        {
            TempData["Status"] = text["Status_CannotEditOwnRoles"].Value;
            return RedirectToAction(nameof(MemberRoles), new { unit, userId });
        }

        var change = await userRoleCommands.SetRolesAsync(
            userId, viewModel.SelectedRoleIds, cancellationToken);

        TempData["Status"] = change switch
        {
            { Failed: true } => text["Status_AssignRolesFailed"].Value,
            { Changed: false } => text["Status_NoChange"].Value,
            _ => text["Status_UserRolesUpdated"].Value
        };

        return RedirectToAction(nameof(MemberRoles), new { unit, userId });
    }

    private async Task<UserRolesViewModel?> MemberRolesModelAsync(
        Guid unit, Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return null;

        var assigned = user.UserRoles.Select(link => link.RoleId).ToHashSet();
        return new UserRolesViewModel
        {
            UserId = userId,
            OrganizationId = unit,
            DisplayName = user.FullName ?? user.Email ?? userId.ToString(),
            Email = user.Email,
            IsSelf = userId == ActorId(),
            Roles = (await roles.GetAllAsync(cancellationToken)).Select(role => new RoleChoice
            {
                Id = role.Id,
                Name = DisplayNames.Role(text, role.Name, role.NameEn, role.IsSystem),
                Description = DisplayNames.RoleDescription(
                    text, role.Name, role.Description, role.DescriptionEn, role.IsSystem),
                IsAssigned = assigned.Contains(role.Id)
            }).ToList()
        };
    }

    // ── الداخل ───────────────────────────────────────────────────────────

    private async Task<OrgMembersViewModel> MembersModelAsync(
        Guid organizationId,
        OrgAccess access,
        OrgHeaderViewModel header,
        string? search,
        CancellationToken cancellationToken)
    {
        var cards = await workspace.GetMemberCardsAsync(organizationId, ActorId(), cancellationToken);
        var platformRoles = await userRoles.GetRolesByUsersAsync(
            cards.Select(card => card.UserId).ToList(), cancellationToken);

        var matches = cards
            .Where(card => Matches(card, search))
            .Select(card => Row(card, access, RoleNames(platformRoles, card.UserId)))
            .ToList();

        return new OrgMembersViewModel
        {
            Header = header,
            Rows = matches,
            Candidates = access.CanManage
                ? [.. await workspace.GetInviteCandidatesAsync(organizationId, cancellationToken)]
                : [],
            Roles = OrgMemberRoles.Admin is var _ ? RoleOptions() : [],
            CountLabel = text["OrgView_MemberCount", matches.Count, cards.Count, header.Name],
            Search = search,
            CanManage = access.CanManage,
            CanGovern = access.CanGovern,
            CanEditPlatformRoles = permissions.HasPermission(PermissionNames.UsersEdit),
            Error = TempData["OrgError"] as string,
            Saved = TempData["OrgSaved"] as string
        };
    }

    private static bool Matches(OrgMemberCard card, string? search) =>
        string.IsNullOrWhiteSpace(search)
        || card.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
        || card.Email.Contains(search, StringComparison.OrdinalIgnoreCase);

    private OrgMemberRowViewModel Row(
        OrgMemberCard card, OrgAccess access, IReadOnlyList<string> platformRoles) => new()
    {
        UserId = card.UserId,
        Name = card.Name,
        Initials = NameInitials.Of(card.Name),
        Email = card.Email,
        UnitId = card.OrganizationId,
        UnitName = card.OrganizationName,
        Role = card.Role,
        RoleLabel = text[$"OrgRole_{card.Role}"],
        RoleClass = card.Role switch
        {
            OrgMemberRoles.Owner => "badge ds-badge-private",
            OrgMemberRoles.Admin => "badge ds-badge-lead",
            _ => "badge text-bg-light"
        },
        StatusLabel = text[$"OrgStatus_{card.Status}"],
        StatusDot = card.Status switch
        {
            OrgMemberStatus.Active => "ds-status-dot--success",
            OrgMemberStatus.Suspended => "ds-status-dot--warning",
            _ => "ds-status-dot--neutral"
        },
        IsSuspended = card.Status == OrgMemberStatus.Suspended,
        // لا يوقف المرء نفسه ولا يزيلها: الخروج من الإدارة ليس زرّاً في جدولها.
        MayAct = access.CanManage && !card.IsSelf,
        PlatformRoles = [.. platformRoles]
    };

    private List<TaskOptionViewModel> RoleOptions() =>
        new[] { OrgMemberRoles.Admin, OrgMemberRoles.Member }
            .Select(role => new TaskOptionViewModel
            {
                Value = role,
                Label = text[$"OrgRole_{role}"],
                IsSelected = role == OrgMemberRoles.Member
            }).ToList();

    /// <summary>
    /// النائب دورٌ واحدٌ في المنظّمة: تعيين جديدٍ يُنزل السابق إلى عضو،
    /// وإلّا اجتمع نائبان ولا يقول التصميم أيّهما.
    /// </summary>
    private async Task SetDeputyAsync(
        Guid organizationId, Guid? deputyId, CancellationToken cancellationToken)
    {
        var members = await workspace.GetMemberCardsAsync(organizationId, Guid.Empty, cancellationToken);
        var current = members.FirstOrDefault(member =>
            member.Role == OrgMemberRoles.Admin && member.OrganizationId == organizationId);

        if (current?.UserId == deputyId) return;

        if (current is not null)
        {
            await memberCommands.ChangeRoleAsync(
                organizationId, current.UserId, OrgMemberRoles.Member, cancellationToken);
        }

        if (deputyId is { } next)
        {
            await memberCommands.ChangeRoleAsync(
                organizationId, next, OrgMemberRoles.Admin, cancellationToken);
        }
    }

    private async Task<IActionResult> TabAsync(
        Guid? unit,
        string tab,
        CancellationToken cancellationToken,
        Func<Organization, OrgAccess, OrgHeaderViewModel, Task<IActionResult>> render)
    {
        var actorId = ActorId();
        var isPlatformAdmin = permissions.HasPermission(PermissionNames.UsersEdit);
        var targets = await workspace.GetSwitchTargetsAsync(
            actorId, isPlatformAdmin, unit ?? Guid.Empty, cancellationToken);

        // بلا وحدةٍ مطلوبة نبدأ من أعلى ما يديره — وهو ما يراه المالك أوّل دخوله.
        var organizationId = unit ?? targets.FirstOrDefault()?.Id;
        if (organizationId is not { } id) return Forbid();

        var access = await workspace.GetAccessAsync(id, actorId, isPlatformAdmin, cancellationToken);
        if (!access.CanManage) return Forbid();

        var organization = await organizations.GetByIdAsync(id, cancellationToken);
        if (organization is null) return NotFound();

        if (unit is null)
        {
            targets = await workspace.GetSwitchTargetsAsync(
                actorId, isPlatformAdmin, id, cancellationToken);
        }

        var header = OrgHeaderBuilder.Build(
            text,
            organization,
            await organizations.CountChildrenAsync(id, cancellationToken),
            targets,
            tab);

        ViewData["Title"] = organization.Name;
        return await render(organization, access, header);
    }

    private async Task<IActionResult> CommandAsync(
        Guid unit,
        string action,
        CancellationToken cancellationToken,
        Func<Organization, OrgAccess, Task> command)
    {
        var actorId = ActorId();
        var access = await workspace.GetAccessAsync(
            unit, actorId, permissions.HasPermission(PermissionNames.UsersEdit), cancellationToken);
        if (!access.CanManage) return Forbid();

        var organization = await organizations.GetByIdAsync(unit, cancellationToken);
        if (organization is null) return NotFound();

        try
        {
            await command(organization, access);
            TempData["OrgSaved"] = text["OrgView_Saved"].Value;
        }
        catch (Exception exception) when (exception is DomainException or InvalidOperationException or ArgumentException)
        {
            TempData["OrgError"] = exception.Message;
        }

        return RedirectToAction(action, new { unit });
    }

    private Task<IActionResult> MemberCommandAsync(
        Guid unit,
        CancellationToken cancellationToken,
        Func<OrgAccess, Guid, Task> command) =>
        CommandAsync(unit, nameof(Members), cancellationToken,
            (organization, access) => command(access, ActorId()));

    private string Assignable(string role) =>
        OrgMemberRoles.IsKnown(role) && role != OrgMemberRoles.Owner
            ? role
            : OrgMemberRoles.Member;

    private OrgStatViewModel Stat(int value, string key, string cssClass) => new()
    {
        Value = value.ToString(),
        Label = text[key],
        CssClass = cssClass
    };

    private List<string> RoleNames(Dictionary<Guid, List<Role>> roles, Guid userId) =>
        roles.TryGetValue(userId, out var mine)
            ? mine.Select(role => DisplayNames.Role(text, role.Name, role.NameEn, role.IsSystem)).ToList()
            : [];

    /// <summary>
    /// سطر النشاط عبارةٌ واحدة بثلاثة مواضع — الفاعل والكيان والقيمة — لا ثلاث
    /// كلماتٍ تُرصّ. ترتيبها يختلف بين اللغتين، ولذلك كان المفتاح واحداً.
    /// </summary>
    private OrgActivityRowViewModel Activity(OrgActivityEntry entry) => new()
    {
        Text = text[$"Activity_{entry.Action}",
            string.IsNullOrWhiteSpace(entry.ActorName) ? text["Activity_System"].Value : entry.ActorName,
            text[$"Activity_Entity_{entry.EntityType}"].Value,
            ActivityPayload.Describe(text, entry.EntityType, entry.Payload)],
        Time = entry.CreatedAt.Relative(text),
        Accent = entry.Action switch
        {
            ActivityActions.Created => "ds-timeline-dot--success",
            ActivityActions.Assigned or ActivityActions.Updated => "ds-timeline-dot--info",
            ActivityActions.StatusChanged => "ds-timeline-dot--warning",
            ActivityActions.Removed or ActivityActions.Archived => "ds-timeline-dot--danger",
            _ => "ds-timeline-dot--neutral"
        }
    };

    private Guid ActorId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
}
