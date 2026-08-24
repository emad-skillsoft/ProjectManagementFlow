using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
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
    private readonly IStringLocalizer<Messages> _text;

    public UsersController(
        IUserQueryService userQueries,
        IRoleQueryService roleQueries,
        IUserRoleCommandService userRoleCommands,
        IStringLocalizer<Messages> text)
    {
        _userQueries = userQueries;
        _roleQueries = roleQueries;
        _userRoleCommands = userRoleCommands;
        _text = text;
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
            Roles = u.UserRoles
                .Select(ur => DisplayNames.Role(_text, ur.Role.Name, ur.Role.NameEn, ur.Role.IsSystem))
                .OrderBy(name => name)
                .ToList()
        }).ToList());
    }

    [HttpGet]
    [RequirePermission(PermissionNames.UsersEdit)]
    public async Task<IActionResult> Roles(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userQueries.GetByIdAsync(id, cancellationToken);
        if (user is null) return NotFound();

        return View(await BuildRolesViewModelAsync(user.Id, user, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionNames.UsersEdit)]
    public async Task<IActionResult> Roles(Guid id, UserRolesViewModel viewModel, CancellationToken cancellationToken)
    {
        var user = await _userQueries.GetByIdAsync(id, cancellationToken);
        if (user is null) return NotFound();

        if (IsSelf(id))
        {
            TempData["Status"] = _text["Status_CannotEditOwnRoles"].Value;
            return RedirectToAction(nameof(Roles), new { id });
        }

        var change = await _userRoleCommands.SetRolesAsync(
            id, viewModel.SelectedRoleIds, cancellationToken);

        TempData["Status"] = change switch
        {
            { Failed: true } => _text["Status_AssignRolesFailed"].Value,
            { Changed: false } => _text["Status_NoChange"].Value,
            _ => _text["Status_UserRolesUpdated"].Value
        };

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
                Name = DisplayNames.Role(_text, r.Name, r.NameEn, r.IsSystem),
                Description = DisplayNames.RoleDescription(_text, r.Name, r.Description, r.DescriptionEn, r.IsSystem),
                IsAssigned = assigned.Contains(r.Id)
            }).ToList()
        };
    }

    // helper
    private bool IsSelf(Guid userId)
           => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)
              && currentUserId == userId;

}
