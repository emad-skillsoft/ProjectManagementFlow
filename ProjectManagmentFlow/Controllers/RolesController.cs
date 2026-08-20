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
    private readonly IStringLocalizer<Messages> _text;

    public RolesController(
        IRoleQueryService roleQueries,
        IRoleCommandService roleCommands,
        IPermissionCatalog permissionCatalog,
        IStringLocalizer<Messages> text)
    {
        _roleQueries = roleQueries;
        _roleCommands = roleCommands;
        _permissionCatalog = permissionCatalog;
        _text = text;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var summaries = await _roleQueries.GetSummariesAsync(cancellationToken);

        return View(summaries.Select(s => new RoleListItemViewModel
        {
            Id = s.Id,
            Name = DisplayNames.Role(_text, s.Name, s.NameEn, s.IsSystem),
            Description = DisplayNames.RoleDescription(_text, s.Name, s.Description, s.DescriptionEn, s.IsSystem),
            IsSystem = s.IsSystem,
            PermissionCount = s.PermissionCount,
            MemberCount = s.MemberCount
        }).ToList());
    }

    [HttpGet]
    [RequirePermission(PermissionNames.RolesManage)]
    public IActionResult Create() => View("Form", new RoleFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionNames.RolesManage)]
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
    [RequirePermission(PermissionNames.RolesManage)]
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
    [RequirePermission(PermissionNames.RolesManage)]
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
    [RequirePermission(PermissionNames.RolesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _roleCommands.DeleteAsync(id, cancellationToken);
        TempData["Status"] = (deleted ? _text["Status_RoleDeleted"] : _text["Status_RoleNotFound"]).Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Permissions(Guid id, CancellationToken cancellationToken)
    {
        var role = await _roleQueries.GetByIdAsync(id, cancellationToken);
        if (role is null) return NotFound();

        var granted = (await _roleQueries.GetPermissionsByRoleAsync(id, cancellationToken))
            .Select(p => p.Id)
            .ToHashSet();

        var all = await _permissionCatalog.GetAllAsync(cancellationToken);

        return View(new RolePermissionsViewModel
        {
            RoleId = role.Id,
            RoleName = DisplayNames.Role(_text, role.Name, role.NameEn, role.IsSystem),
            Permissions = all.Select(p => new PermissionChoice
            {
                Id = p.Id,
                Name = p.Name,
                Description = DisplayNames.Permission(_text, p.Name, p.Description),
                IsGranted = granted.Contains(p.Id)
            }).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionNames.RolesManage)]
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
            return RedirectToAction(nameof(Permissions), new { id });
        }

        if (toRevoke.Count > 0)
        {
            await _roleCommands.RevokePermissionsAsync(id, toRevoke, cancellationToken);
        }

        TempData["Status"] = toGrant.Count + toRevoke.Count == 0
            ? _text["Status_NoChange"].Value
            : _text["Status_RolePermissionsUpdated"].Value;

        return RedirectToAction(nameof(Permissions), new { id });
    }
}
