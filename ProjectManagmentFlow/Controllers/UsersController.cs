using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ProjectManagmentFlow.Authorization;
using ProjectManagmentFlow.Filters;
using ProjectManagmentFlow.Services.Roles;
using ProjectManagmentFlow.Services.Users;
using ProjectManagmentFlow.ViewModels;

namespace ProjectManagmentFlow.Controllers;

[RequirePermission(PermissionNames.UsersView)]
public class UsersController : Controller
{
    private readonly IUserQueryService _userQueries;
    private readonly IRoleQueryService _roleQueries;
    private readonly IUserRoleCommandService _userRoleCommands;

    public UsersController(
        IUserQueryService userQueries,
        IRoleQueryService roleQueries,
        IUserRoleCommandService userRoleCommands)
    {
        _userQueries = userQueries;
        _roleQueries = roleQueries;
        _userRoleCommands = userRoleCommands;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var users = await _userQueries.GetAllAsync(cancellationToken);

        return View(users.Select(u => new UserListItemViewModel
        {
            Id = u.Id,
            DisplayName = u.FullName ?? u.Email ?? u.Id.ToString(),
            Email = u.Email,
            IsActive = u.IsActive,
            IsLockedOut = u.LockoutEndUtc > DateTime.UtcNow,
            LastSeenAt = u.LastSeenAt,
            Roles = u.UserRoles.Select(ur => ur.Role.Name).OrderBy(name => name).ToList()
        }).ToList());
    }

    [HttpGet]
    [RequirePermission(PermissionNames.UsersManage)]
    public async Task<IActionResult> Roles(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userQueries.GetByIdAsync(id, cancellationToken);
        if (user is null) return NotFound();

        return View(await BuildRolesViewModelAsync(user.Id, user, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionNames.UsersManage)]
    public async Task<IActionResult> Roles(Guid id, UserRolesViewModel viewModel, CancellationToken cancellationToken)
    {
        var user = await _userQueries.GetByIdAsync(id, cancellationToken);
        if (user is null) return NotFound();

        if (IsSelf(id))
        {
            TempData["Status"] = "لا يمكنك تعديل أدوار حسابك — اطلب ذلك من مدير آخر.";
            return RedirectToAction(nameof(Roles), new { id });
        }

        var selected = viewModel.SelectedRoleIds.Distinct().ToList();
        var current = user.UserRoles.Select(ur => ur.RoleId).ToList();

        var toAssign = selected.Except(current).ToList();
        var toRemove = current.Except(selected).ToList();

        if (toAssign.Count > 0 && !await _userRoleCommands.AssignRolesToUserAsync(id, toAssign, cancellationToken))
        {
            TempData["Status"] = "تعذّر إسناد بعض الأدوار — ربّما حُذفت.";
            return RedirectToAction(nameof(Roles), new { id });
        }

        if (toRemove.Count > 0)
        {
            await _userRoleCommands.RemoveRolesFromUserAsync(id, toRemove, cancellationToken);
        }

        TempData["Status"] = toAssign.Count + toRemove.Count == 0
            ? "لا تغيير."
            : "تم تحديث أدوار المستخدم، وأُبطلت جلسته القائمة.";

        return RedirectToAction(nameof(Roles), new { id });
    }

    private async Task<UserRolesViewModel> BuildRolesViewModelAsync(
        Guid userId,
        Models.User user,
        CancellationToken cancellationToken)
    {
        var assigned = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();
        var allRoles = await _roleQueries.GetAllAsync(cancellationToken);

        return new UserRolesViewModel
        {
            UserId = userId,
            DisplayName = user.FullName ?? user.Email ?? userId.ToString(),
            Email = user.Email,
            IsSelf = IsSelf(userId),
            Roles = allRoles.Select(r => new RoleChoice
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                IsAssigned = assigned.Contains(r.Id)
            }).ToList()
        };
    }

    // helper
    private bool IsSelf(Guid userId)
           => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)
              && currentUserId == userId;

}
