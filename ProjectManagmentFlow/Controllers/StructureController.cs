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
using ProjectManagmentFlow.Services.Projects;
using ProjectManagmentFlow.ViewModels;

namespace ProjectManagmentFlow.Controllers;

/// <summary>
/// صفحة «الهيكل»: شجرة المنظّمة، ولوحة كل وحدة بأعضائها ومشاريعها.
/// [RequirePermission(OrganizationsView)] + OrgAccess.CanManage (مالك · نائب ·
/// أدمن المنصّة). يرى المنظّمة وما يتبعها فقط — كما في
/// OrgWorkspaceService.GetSwitchTargetsAsync. غيره: Forbid.
/// </summary>
[RequirePermission(PermissionNames.OrganizationsView)]
public sealed class StructureController(
    IOrganizationQueryService organizations,
    IOrganizationCommandService organizationCommands,
    IOrganizationMemberQueryService memberQueries,
    IOrganizationMemberCommandService memberCommands,
    IOrgWorkspaceService workspace,
    IProjectQueryService projects,
    IPermissionService permissions,
    IStringLocalizer<Messages> text) : Controller
{
    /// <summary>
    /// وضع العرض للطلب الحاليّ. المتحكّم يُنشأ لكلّ طلب، والقيمة تُقرأ مرّةً في
    /// PageAsync لتبني كلّ روابط الصفحة — فالتنقّل لا يُخرجك من المخطّط إلى القائمة.
    /// </summary>
    private string currentView = ListView;

    private const string ChartView = "chart";
    private const string ListView = "list";

    private string UnitHref(Guid unitId, string tab = "overview") =>
        currentView == ChartView
            ? $"/Structure?unit={unitId}&tab={tab}&view={ChartView}"
            : $"/Structure?unit={unitId}&tab={tab}";

    [HttpGet("/Structure")]
    public async Task<IActionResult> Index(
        Guid? unit, string? view, string? tab, string? scope,
        CancellationToken cancellationToken) => await PageAsync(
        unit, view ?? "list", tab ?? "overview", scope ?? "own", cancellationToken);

    [HttpPost("/Structure/units")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create(
        Guid parentId, string? name, string? code, string? type,
        CancellationToken cancellationToken) =>
        // الإنشاء يقع على الأب: من يديره يضيف تحته.
        PostAsync(parentId, cancellationToken, async () =>
        {
            var created = await organizationCommands.CreateAsync(
                name ?? string.Empty, null, parentId, type ?? OrgUnitTypes.Organization,
                code, ActorId(), cancellationToken);
            TempData["StructureStatus"] = text["Structure_Created", created.Name, Label(created.Type)].Value;
            return created.Id;
        });

    [HttpPost("/Structure/units/{id}/move")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Move(Guid id, Guid? targetParentId, CancellationToken cancellationToken) =>
        PostAsync(id, cancellationToken, async () =>
        {
            var targets = await organizationCommands.GetMoveTargetsAsync(
                id, ActorId(), IsPlatformAdmin(), cancellationToken);
            var target = targetParentId is null
                ? targets.FirstOrDefault(t => t.TargetId is null && t.IsAllowed)
                : targets.FirstOrDefault(t => t.TargetId == targetParentId);

            if (target is not { IsAllowed: true })
            {
                throw new DomainException(
                    target?.ReasonKey is { Length: > 0 } reason
                        ? text[reason, (object[])target.ReasonArgs].Value
                        : text["Structure_MoveFailed"].Value);
            }

            await organizationCommands.MoveAsync(id, targetParentId, cancellationToken);
            TempData["StructureStatus"] = text["Structure_Moved"].Value;
            return id;
        });

    [HttpPost("/Structure/units/{id}/delete")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        PostAsync(id, cancellationToken, async () =>
        {
            var unit = await organizations.GetByIdAsync(id, cancellationToken)
                ?? throw new DomainException(text["Org_NotFound"].Value);

            var blocker = await organizationCommands.GetDeleteBlockerAsync(id, cancellationToken);
            if (blocker is not null) throw new DomainException(blocker);

            await organizationCommands.DeleteAsync(id, cancellationToken);
            // بعد الحذف نعود لأقرب جدٍّ قائم.
            var parent = unit.ParentId is { } pid
                ? await organizations.GetByIdAsync(pid, cancellationToken)
                : null;
            return parent?.Id
                ?? (await FirstManagedRootAsync(cancellationToken))
                ?? id;
        });

    [HttpPost("/Structure/units/{id}/members/{userId}/role")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SetMemberRole(
        Guid id, Guid userId, string? role, CancellationToken cancellationToken) =>
        PostAsync(id, cancellationToken, async () =>
        {
            // تعيين الأدوار: admin أو member. owner ملكيّته تُنقل لا تُعطى.
            var assignable = role == OrgMemberRoles.Admin ? OrgMemberRoles.Admin : OrgMemberRoles.Member;
            await memberCommands.ChangeRoleAsync(id, userId, assignable, cancellationToken);
            TempData["StructureStatus"] = text["Structure_RoleChanged"].Value;
            return id;
        });

    // ── بناء الصفحة ───────────────────────────────────────────────────────

    private async Task<IActionResult> PageAsync(
        Guid? unit, string view, string tab, string scope, CancellationToken cancellationToken)
    {
        currentView = view == ChartView ? ChartView : ListView;

        var managesAny = await workspace.ManagesAnyAsync(
            ActorId(), IsPlatformAdmin(), cancellationToken);
        var organizationId = unit ?? (await FirstManagedRootAsync(cancellationToken));

        if (organizationId is not { } id)
        {
            // يرى المنظّمات لكنه لا يدير أيّة — لا هيكل له.
            return managesAny ? Forbid() : Forbid();
        }

        var access = await workspace.GetAccessAsync(id, ActorId(), IsPlatformAdmin(), cancellationToken);
        if (!access.CanManage) return Forbid();

        var selected = await organizations.GetByIdAsync(id, cancellationToken);
        if (selected is null) return NotFound();

        // الشجرة من أعلى ما يديره الفاعل لا من جذر المنظّمة: مالك إدارةٍ تابعة
        // لا يرى أسماء الفروع الشقيقة ولا رموزها ولا عدّاداتها.
        var scopeRootId = await ScopeRootAsync(id, cancellationToken);
        var tree = await organizations.GetTreeAsync(scopeRootId, cancellationToken);
        if (tree is null) return NotFound();

        var meta = text["Structure_SummaryMeta", FlatCount(tree), Organization.MaxDepth].Value;

        ViewData["Breadcrumb"] = await BuildBreadcrumbAsync(
            id, scopeRootId, selected.Name, cancellationToken);

        return View(nameof(Index), new StructureViewModel
        {
            IsChartView = view == "chart",
            Status = TempData["StructureStatus"] as string,
            Error = TempData["StructureError"] as string,
            View = new StructureViewSwitchViewModel
            {
                Label = text["Structure_ViewSwitch"].Value,
                Meta = meta,
                Options =
                [
                    new() { Label = text["Structure_ViewList"].Value, Value = "list", IsSelected = view != "chart" },
                    new() { Label = text["Structure_ViewChart"].Value, Value = "chart", IsSelected = view == "chart" }
                ]
            },
            Tree = new StructureTreePanelViewModel
            {
                Label = text["Structure_TreeLabel"].Value,
                UnitCountLabel = meta,
                SearchLabel = text["Structure_Search"].Value,
                ExpandAllLabel = text["Structure_ExpandAll"].Value,
                CollapseAllLabel = text["Structure_CollapseAll"].Value,
            ToggleLabel = text["Structure_ToggleUnit"].Value,
                SelectedUnitId = id,
                Search = string.Empty,
                AllExpanded = true,
                Root = ToTreeNode(tree, id, expanded: true)
            },
            Chart = new StructureChartPanelViewModel
            {
                Label = text["Structure_ChartLabel"].Value,
                Hint = text["Structure_ChartHint"].Value,
                Root = ToChartNode(tree, id),
                Toolbar = new StructureChartToolbarViewModel
                {
                    SearchLabel = text["Structure_Search"].Value,
                    ExpandAllLabel = text["Structure_ExpandAll"].Value,
                    CollapseAllLabel = text["Structure_CollapseAll"].Value,
                    ZoomInLabel = text["Structure_ZoomIn"].Value,
                    ZoomOutLabel = text["Structure_ZoomOut"].Value,
                    FitLabel = text["Structure_Fit"].Value,
                    ZoomPercent = 100
                }
            },
            Panel = await BuildPanelAsync(selected, access, tab, scope == "subtree", cancellationToken)
        });
    }

    private async Task<StructureUnitPanelViewModel> BuildPanelAsync(
        Organization selected, OrgAccess access, string tab, bool scopeSubtree,
        CancellationToken cancellationToken)
    {
        var unitId = selected.Id;
        var stats = await organizations.GetUnitStatsAsync(unitId, cancellationToken);
        var children = await organizations.GetChildrenAsync(unitId, cancellationToken);

        var directUnits = children
            .OrderBy(child => child.Name, StringComparer.CurrentCulture)
            .Select(child => new StructureDirectUnitViewModel
            {
                UnitId = child.Id,
                Name = child.Name,
                Code = child.Code,
                Type = Badge(child),
                DirectProjects = child.Projects.Count(p => p.DeletedAt == null),
                Href = UnitHref(child.Id)
            }).ToList();

        // الأعضاء — مباشرون (قابلو التعديل) وموروَّثون (مقروءون، بشارة مصدرهم).
        var cards = await workspace.GetMemberCardsAsync(unitId, ActorId(), cancellationToken);
        var directMembers = cards
            .Where(card => card.OrganizationId == unitId)
            .OrderBy(card => card.Role, StringComparer.Ordinal)
            .ThenBy(card => card.Name, StringComparer.CurrentCulture)
            .Select(card => new StructureMemberViewModel
            {
                UserId = card.UserId,
                Name = card.Name,
                Email = card.Email,
                Role = card.Role,
                RoleLabel = text[$"OrgRole_{card.Role}"].Value,
                IsInherited = false,
                Initials = NameInitials.Of(card.Name),
                // المالك خارج القائمة: «مالك» ليس خياراً فيها، فعرضها له يُظهره
                // «عضواً» ويدعو إلى إجراءٍ يردّه الخادم.
                MayChangeRole = access.CanManage
                                && !card.IsSelf
                                && card.Role != OrgMemberRoles.Owner
            }).ToList();

        var inheritedMembers = (await memberQueries.GetInheritedMembersAsync(unitId, cancellationToken))
            .OrderBy(member => member.SourceUnitName, StringComparer.CurrentCulture)
            .Select(member => new StructureMemberViewModel
            {
                UserId = member.UserId,
                Name = member.Name,
                Email = member.Email,
                Role = member.Role,
                RoleLabel = text[$"OrgRole_{member.Role}"].Value,
                Initials = NameInitials.Of(member.Name),
                IsInherited = true,
                InheritedFromName = member.SourceUnitName,
                MayChangeRole = false
            }).ToList();

        // المشاريع — «هذه الوحدة» أو «وما تحتها». بطاقات جاهزة للعرض كما في صفحة المشاريع.
        var projectCards = await projects.GetByOrgAsync(
            unitId, scopeSubtree, ProjectScope.Active, null, cancellationToken);
        var projectViewModels = projectCards.Select(BuildProjectCard).ToList();

        var tabKey = tab is "members" or "projects" ? tab : "overview";

        return new StructureUnitPanelViewModel
        {
            UnitId = unitId,
            Name = selected.Name,
            Type = Badge(selected),
            Code = selected.Code,
            Depth = selected.Depth,
            MaxDepth = Organization.MaxDepth,
            LevelLabel = text["Structure_Level", OrgUnitTypes.GetRank(selected.Type) + 1, Organization.MaxDepth].Value,
            DirectChildren = children.Count,
            MayManage = access.CanManage,
            Tabs =
            [
                Tab("overview", "Structure_TabOverview", tabKey),
                Tab("members", "Structure_TabMembers", tabKey),
                Tab("projects", "Structure_TabProjects", tabKey)
            ],
            Stats =
            [
                Stat(stats.DirectProjects.ToString(), "Structure_StatProjects"),
                Stat(stats.SubtreeProjects.ToString(), "Structure_StatSubtreeProjects"),
                Stat(stats.SubtreeUnits.ToString(), "Structure_StatDirectUnits"),
                Stat((stats.DirectMembers + stats.InheritedMembers).ToString(), "Structure_StatMembers")
            ],
            DirectUnits = directUnits,
            Members = directMembers,
            InheritedMembers = inheritedMembers,
            Projects = projectViewModels,
            IncludeDescendants = scopeSubtree,
            Create = BuildCreateDialog(selected),
            Move = await BuildMoveDialogAsync(unitId, cancellationToken),
            DeleteDialog = await BuildDeleteDialogAsync(unitId, selected, cancellationToken)
        };

        StructureTabViewModel Tab(string key, string labelKey, string current) => new()
        {
            Key = key,
            Label = text[labelKey].Value,
            Href = UnitHref(unitId, key),
            IsCurrent = key == current
        };
    }

    private ProjectCardViewModel BuildProjectCard(ProjectCard card)
    {
        var ownerName = string.IsNullOrWhiteSpace(card.OwnerName) ? "—" : card.OwnerName.Trim();
        var percent = card.TotalTasks == 0
            ? 0
            : (int)Math.Round(card.DoneTasks * 100d / card.TotalTasks);
        var today = DateOnly.FromDateTime(DisplayTime.RiyadhNow());

        return new ProjectCardViewModel
        {
            Id = card.Id,
            Code = card.Code,
            Name = card.Name,
            Description = card.Description,
            StatusLabel = text[$"ProjectStatus_{card.Status}"].Value,
            StatusClass = ProjectPresentation.StatusClass(card.Status),
            StatusBadgeClass = ProjectPresentation.StatusBadgeClass(card.Status),
            PriorityLabel = text[$"ProjectPriority_{card.Priority}"].Value,
            PriorityClass = $"ds-priority ds-priority--{card.Priority}",
            UnitName = card.OrganizationName ?? "—",
            UnitTypeLabel = text[$"OrgType_{card.OrganizationType}"].Value,
            UnitIsRoot = card.OrganizationDepth == 0 && card.OrganizationType == "organization",
            OwnerName = ownerName,
            OwnerInitial = NameInitials.Of(ownerName),
            DueLabel = card.DueDate is { } due
                ? text["Projects_Due", due.Local()].Value
                : text["Projects_NoDue"].Value,
            IsOverdue = card.DueDate is { } dueDate
                        && dueDate < today
                        && card.Status != ProjectStatus.Done,
            ProgressLabel = $"{card.DoneTasks}/{card.TotalTasks}",
            HasTasks = card.TotalTasks > 0,
            Percent = Math.Clamp(percent, 0, 100),
            Href = $"/projects/{card.Id}"
        };
    }

    private StructureCreateDialogViewModel BuildCreateDialog(Organization parent) => new()
    {
        Label = text["Structure_CreateTitle"].Value,
        NameLabel = text["Structure_Name"].Value,
        CodeLabel = text["Structure_Code"].Value,
        TypeLabel = text["Structure_Type"].Value,
        SubmitLabel = text["Structure_CreateSubmit"].Value,
        ParentId = parent.Id.ToString(),
        // الأنواع المسموحة محسوبة من رتبة الأب — الأعلى رقماً فقط.
        AllowedTypes = OrgUnitTypes.AllowedChildTypes(parent.Type)
            .Select((code, index) => new StructureTypeOptionViewModel
            {
                Value = code,
                Label = text[$"OrgType_{code}"].Value,
                IsSelected = index == 0
            }).ToList()
    };

    private async Task<StructureMoveDialogViewModel> BuildMoveDialogAsync(
        Guid unitId, CancellationToken cancellationToken)
    {
        var targets = await organizationCommands.GetMoveTargetsAsync(
            unitId, ActorId(), IsPlatformAdmin(), cancellationToken);

        return new StructureMoveDialogViewModel
        {
            UnitId = unitId,
            Label = text["Structure_MoveTitle"].Value,
            Summary = text["Structure_MoveSummary"].Value,
            SubmitLabel = text["Structure_MoveSubmit"].Value,
            ImpactLabel = text["Structure_MoveImpact"].Value,
            Targets = targets.Select(target => new StructureMoveOptionViewModel
            {
                TargetId = target.TargetId,
                Name = target.Name,
                IsAllowed = target.IsAllowed,
                Reason = target.ReasonKey is { Length: > 0 } key
                    ? text[key, (object[])target.ReasonArgs].Value
                    : null
            }).ToList()
        };
    }

    private async Task<StructureDeleteDialogViewModel> BuildDeleteDialogAsync(
        Guid unitId, Organization selected, CancellationToken cancellationToken)
    {
        var blocker = await organizationCommands.GetDeleteBlockerAsync(unitId, cancellationToken);
        return new StructureDeleteDialogViewModel
        {
            UnitId = unitId,
            Label = text["Structure_DeleteTitle"].Value,
            ConfirmLabel = text["Structure_DeleteConfirm", selected.Name].Value,
            UnitName = selected.Name,
            Blocker = blocker
        };
    }

    // ── أدوات ────────────────────────────────────────────────────────────

    /// <summary>
    /// كلّ أمرٍ يقع على وحدةٍ بعينها، وصلاحيّتها تُفحص هنا قبل تنفيذه.
    /// الحارس في موضعٍ واحد لا في أربعة، لأنّ أمراً جديداً يُضاف غداً
    /// سيمرّ من هنا حتماً — و«نسيتُ الفحص» أشيع من «كتبتُه خطأ».
    /// [RequirePermission(OrganizationsView)] لا يكفي: كلّ الأدوار تملكها.
    /// </summary>
    private Task<IActionResult> PostAsync(
        Guid unitId, CancellationToken cancellationToken, Func<Task<Guid?>> command)
    {
        return PostAsyncCore(command);

        async Task<IActionResult> PostAsyncCore(Func<Task<Guid?>> c)
        {
            try
            {
                var access = await workspace.GetAccessAsync(
                    unitId, ActorId(), IsPlatformAdmin(), cancellationToken);
                if (!access.CanManage) return Forbid();

                var returnUnit = await c();
                return Redirect($"/Structure?unit={returnUnit}&tab=overview");
            }
            catch (DomainException exception)
            {
                // رُفعت عمداً (تعارض، رتبة، عمق…) — نعرض سببها.
                TempData["StructureError"] = exception.Message;
                return Redirect("/Structure");
            }
            catch (ArgumentException exception)
            {
                // حارس إنشاء/تحديث — نفس المعاملة.
                TempData["StructureError"] = exception.Message;
                return Redirect("/Structure");
            }
            catch (InvalidOperationException exception)
            {
                // حارس رتبة/عمق/نطاق النقل والحذف.
                TempData["StructureError"] = exception.Message;
                return Redirect("/Structure");
            }
        }
    }

    /// <summary>
    /// مسار الوحدة: المنظّمة أوّلاً ثمّ سلسلة الأجداد مطويّةً ثمّ الوحدة نفسها.
    /// GetAncestorsAsync تُرجع الوحدة ضمن السلسلة، فتُستثنى من الأجداد لئلّا تتكرّر.
    /// </summary>
    private async Task<AppBreadcrumbViewModel> BuildBreadcrumbAsync(
        Guid unitId, Guid scopeRootId, string unitName, CancellationToken cancellationToken)
    {
        var chain = await organizations.GetAncestorsAsync(unitId, cancellationToken);

        // المسار يبدأ من منطلق نطاق الفاعل لا من جذر المنظّمة: ذكر جدٍّ لا يبلغه
        // يكشف اسمه ويعطيه رابطاً يُردّ عنه.
        var scopeIndex = chain.FindIndex(unit => unit.Id == scopeRootId);
        var ancestors = chain
            .Skip(scopeIndex < 0 ? 0 : scopeIndex)
            .SkipLast(1)
            .ToList();

        return BreadcrumbBuilder.ForUnit(
            text, ancestors, unitName,
            id => UnitHref(id));
    }

    private int FlatCount(OrganizationUnitNode root)
    {
        int Count(OrganizationUnitNode node) => 1 + node.Children.Sum(Count);
        return Count(root);
    }

    private StructureTreeNodeViewModel ToTreeNode(OrganizationUnitNode node, Guid currentId, bool expanded) => new()
    {
        UnitId = node.Id,
        Name = node.Name,
        Code = node.Code,
        Type = Badge(node),
        DirectProjects = node.DirectProjects,
        Depth = (int)node.Depth,
        IsExpanded = expanded,
        ParentId = node.ParentId,
        Children = [.. node.Children.Select(child => ToTreeNode(child, currentId, expanded))],
        IsCurrent = node.Id == currentId,
        Href = UnitHref(node.Id)
    };

    private StructureChartNodeViewModel ToChartNode(OrganizationUnitNode node, Guid currentId) => new()
    {
        UnitId = node.Id,
        Name = node.Name,
        Code = node.Code,
        Type = Badge(node),
        HasChildren = node.HasChildren,
        IsCurrent = node.Id == currentId,
        IsExpanded = true,
        Href = UnitHref(node.Id),
        Children = [.. node.Children.Select(child => ToChartNode(child, currentId))]
    };

    private UnitTypeBadgeViewModel Badge(Organization unit) => new()
    {
        Label = text[$"OrgType_{unit.Type}"].Value,
        IsRoot = unit.Depth == 0 && unit.Type == OrgUnitTypes.Organization
    };

    private UnitTypeBadgeViewModel Badge(OrganizationUnitNode node) => new()
    {
        Label = text[$"OrgType_{node.Type}"].Value,
        IsRoot = node.Depth == 0 && node.Type == OrgUnitTypes.Organization
    };

    private string Label(string type) => text[$"OrgType_{type}"].Value;

    private StructureStatViewModel Stat(string value, string key) => new()
    {
        Value = value,
        Label = text[key].Value
    };

    /// <summary>
    /// أعلى وحدةٍ يديرها الفاعل وتحتوي الوحدة المعروضة — منطلق شجرته.
    /// أدمن المنصّة نطاقه المنظّمة كلّها، فينطلق من جذرها.
    /// </summary>
    private async Task<Guid> ScopeRootAsync(Guid unitId, CancellationToken cancellationToken)
    {
        var chain = await organizations.GetAncestorsAsync(unitId, cancellationToken);
        if (chain.Count == 0) return unitId;

        if (IsPlatformAdmin()) return chain[0].Id;

        var managed = (await workspace.GetSwitchTargetsAsync(
                ActorId(), false, Guid.Empty, cancellationToken))
            .Select(target => target.Id)
            .ToHashSet();

        return chain.FirstOrDefault(unit => managed.Contains(unit.Id))?.Id ?? unitId;
    }

    private async Task<Guid?> FirstManagedRootAsync(CancellationToken cancellationToken)
    {
        var targets = await workspace.GetSwitchTargetsAsync(
            ActorId(), IsPlatformAdmin(), Guid.Empty, cancellationToken);
        return targets.FirstOrDefault(t => t.IsRoot)?.Id
            ?? targets.FirstOrDefault()?.Id;
    }

    private Guid ActorId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private bool IsPlatformAdmin() => permissions.HasPermission(PermissionNames.UsersEdit);
}
