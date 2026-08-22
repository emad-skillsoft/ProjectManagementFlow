using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Authorization;
using ProjectManagmentFlow.Filters;
using ProjectManagmentFlow.Services.Roles;
using ProjectManagmentFlow.ViewModels;
using ProjectManagmentFlow.Services.Permissions;

namespace ProjectManagmentFlow.Controllers;

[RequirePermission(PermissionNames.RolesView)]
public class RolesController : Controller
{
    private readonly IRoleQueryService _roleQueries;
    private readonly IRoleCommandService _roleCommands;
    private readonly IPermissionCatalog _permissionCatalog;
    private readonly IPermissionService _permissions;
    private readonly IStringLocalizer<Messages> _text;

    public RolesController(
        IRoleQueryService roleQueries,
        IRoleCommandService roleCommands,
        IPermissionCatalog permissionCatalog,
        IPermissionService permissions,
        IStringLocalizer<Messages> text)
    {
        _roleQueries = roleQueries;
        _roleCommands = roleCommands;
        _permissionCatalog = permissionCatalog;
        _permissions = permissions;
        _text = text;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? roleId, CancellationToken cancellationToken)
    {
        var summaries = await _roleQueries.GetSummariesAsync(cancellationToken);
        var selectedId = summaries.Any(s => s.Id == roleId) ? roleId!.Value : summaries.FirstOrDefault()?.Id;

        var cards = summaries.Select(s => new RoleListItemViewModel
        {
            Id = s.Id,
            Name = DisplayNames.Role(_text, s.Name, s.NameEn, s.IsSystem),
            Description = DisplayNames.RoleDescription(_text, s.Name, s.Description, s.DescriptionEn, s.IsSystem),
            IsSystem = s.IsSystem,
            PermissionCount = s.PermissionCount,
            MemberCount = s.MemberCount,
            IsSelected = s.Id == selectedId
        }).ToList();

        SetBreadcrumb();

        return View(new PermissionsPageViewModel
        {
            Roles = cards,
            Selected = selectedId is null ? null : await BuildRoleMatrixAsync(selectedId.Value, cancellationToken)
        });
    }

    [HttpGet]
    public IActionResult Permissions(Guid id) => RedirectToAction(nameof(Index), new { roleId = id });

    [HttpGet]
    [RequirePermission(PermissionNames.RolesCreate)]
    public IActionResult Create() => View("Form", new RoleFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionNames.RolesCreate)]
    public async Task<IActionResult> Create(RoleFormViewModel viewModel, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View("Form", viewModel);

        try
        {
            await _roleCommands.CreateAsync(
                viewModel.Name, viewModel.Description, viewModel.NameEn, viewModel.DescriptionEn, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(viewModel.Name), ex.Message);
            return View("Form", viewModel);
        }

        TempData["Status"] = _text["Status_RoleCreated"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [RequirePermission(PermissionNames.RolesEdit)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var role = await _roleQueries.GetByIdAsync(id, cancellationToken);
        if (role is null) return NotFound();

        return View("Form", new RoleFormViewModel
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            NameEn = role.NameEn,
            DescriptionEn = role.DescriptionEn
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionNames.RolesEdit)]
    public async Task<IActionResult> Edit(Guid id, RoleFormViewModel viewModel, CancellationToken cancellationToken)
    {
        viewModel.Id = id;
        if (!ModelState.IsValid) return View("Form", viewModel);

        var updated = await _roleCommands.UpdateAsync(
            id, viewModel.Name, viewModel.Description, viewModel.NameEn, viewModel.DescriptionEn, cancellationToken);
        if (!updated)
        {
            ModelState.AddModelError(nameof(viewModel.Name), _text["RoleForm_SaveFailed"]);
            return View("Form", viewModel);
        }

        TempData["Status"] = _text["Status_RoleSaved"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionNames.RolesDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _roleCommands.DeleteAsync(id, cancellationToken);
        TempData["Status"] = (deleted ? _text["Status_RoleDeleted"] : _text["Status_RoleNotFound"]).Value;
        return RedirectToAction(nameof(Index));
    }

    private async Task<RolePermissionsViewModel?> BuildRoleMatrixAsync(Guid id, CancellationToken cancellationToken)
    {
        var role = await _roleQueries.GetByIdAsync(id, cancellationToken);
        if (role is null) return null;

        var granted = (await _roleQueries.GetPermissionsByRoleAsync(id, cancellationToken))
            .Select(p => p.Id)
            .ToHashSet();

        var all = await _permissionCatalog.GetAllAsync(cancellationToken);

        var roleName = DisplayNames.Role(_text, role.Name, role.NameEn, role.IsSystem);
        var choices = all.Select(p => new PermissionChoice
        {
            Id = p.Id,
            Name = p.Name,
            Description = DisplayNames.Permission(_text, p.Name, p.Description),
            IsGranted = granted.Contains(p.Id)
        }).ToList();

        var canEdit = _permissions.HasPermission(PermissionNames.RolesEdit);
        var holderCount = (await _roleQueries.GetSummariesAsync(cancellationToken))
            .FirstOrDefault(summary => summary.Id == id)?.MemberCount ?? 0;

        var (matrix, panel) = PermissionMatrixBuilder.Build(_text, roleName, holderCount, choices, canEdit);

        return new RolePermissionsViewModel
        {
            RoleId = role.Id,
            RoleName = roleName,
            Permissions = choices,
            Matrix = matrix,
            Panel = panel
        };
    }

    private void SetBreadcrumb() => ViewData["Breadcrumb"] = new AppBreadcrumbViewModel
    {
        Label = _text["Perm_PageTitle"],
        Items =
        [
            new() { Label = _text["Dashboard_Title"], Url = "/Dashboard" },
            new() { Label = _text["Perm_PageTitle"], IsCurrent = true }
        ]
    };

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionNames.RolesEdit)]
    public async Task<IActionResult> Permissions(Guid id, RolePermissionsViewModel viewModel, CancellationToken cancellationToken)
    {
        var role = await _roleQueries.GetByIdAsync(id, cancellationToken);
        if (role is null) return NotFound();

        var selected = viewModel.SelectedPermissionIds.Distinct().ToList();
        var current = (await _roleQueries.GetPermissionsByRoleAsync(id, cancellationToken))
            .Select(p => p.Id)
            .ToList();

        var toGrant = selected.Except(current).ToList();
        var toRevoke = current.Except(selected).ToList();

        if (toGrant.Count > 0 && !await _roleCommands.AssignPermissionsAsync(id, toGrant, cancellationToken))
        {
            TempData["Status"] = _text["Status_GrantFailed"].Value;
            return RedirectToAction(nameof(Index), new { roleId = id });
        }

        if (toRevoke.Count > 0)
        {
            await _roleCommands.RevokePermissionsAsync(id, toRevoke, cancellationToken);
        }

        TempData["Status"] = toGrant.Count + toRevoke.Count == 0
            ? _text["Status_NoChange"].Value
            : _text["Status_RolePermissionsUpdated"].Value;

        return RedirectToAction(nameof(Index), new { roleId = id });
    }
}
