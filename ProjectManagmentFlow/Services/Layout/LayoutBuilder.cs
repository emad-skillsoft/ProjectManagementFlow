using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Authorization;
using ProjectManagmentFlow.Services.Permissions;
using ProjectManagmentFlow.Services.Users;
using ProjectManagmentFlow.ViewModels;

namespace ProjectManagmentFlow.Services.Layout;

public class LayoutBuilder
{
    private readonly IPermissionService _permissions;
    private readonly IStringLocalizer<Messages> _text;
    private readonly IHttpContextAccessor _http;
    private readonly IUserRoleQueryService _userRoles;

    public LayoutBuilder(
        IPermissionService permissions,
        IStringLocalizer<Messages> text,
        IHttpContextAccessor http,
        IUserRoleQueryService userRoles)
    {
        _permissions = permissions;
        _text = text;
        _http = http;
        _userRoles = userRoles;
    }

    public async Task<LayoutViewModel> BuildAsync(CancellationToken cancellationToken = default)
    {
        var model = LayoutViewModel.CreateDefault(_text);
        var user = _http.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return model;
        }

        var displayName = user.Identity.Name ?? string.Empty;
        model.Header.User = new HeaderUserViewModel
        {
            DisplayName = displayName,
            Initials    = InitialOf(displayName),
            Role        = await ResolveRoleAsync(user, cancellationToken),
            ProfileUrl  = "#"
        };

        var path = _http.HttpContext?.Request?.Path.Value ?? string.Empty;
        model.Header.Items = BuildPrimaryNavigation(path);

        return model;
    }

    public LayoutViewModel BuildForSignIn()
    {
        var model = LayoutViewModel.CreateDefault(_text);

        model.MetaDescription   = _text["App_SignIn"];
        model.Header.Items      = [];
        model.Header.User       = null;
        model.Sidebar.IsVisible = false;

        return model;
    }

    private async Task<string?> ResolveRoleAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var identifier = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(identifier) || !Guid.TryParse(identifier, out var userId))
        {
            return null;
        }

        var roles = await _userRoles.GetRolesByUserAsync(userId, cancellationToken);
        var role = roles.FirstOrDefault(r => !r.IsSystem) ?? roles.FirstOrDefault();
        return role is null
            ? null
            : DisplayNames.Role(_text, role.Name, role.NameEn, role.IsSystem);
    }

    private IReadOnlyList<NavigationItemViewModel> BuildPrimaryNavigation(string path)
    {
        var items = new List<NavigationItemViewModel>
        {
            new()
            {
                Label    = _text["Dashboard_Title"],
                Url      = "/Dashboard",
                IsActive = IsCurrent("/Dashboard", path)
            }
        };

        Add(items, PermissionNames.OrganizationsView, "Nav_Organizations", "/Organizations", path);
        Add(items, PermissionNames.ProjectsView,      "Nav_Projects",      "/Projects",      path);
        Add(items, PermissionNames.TasksView,         "Nav_MyTasks",       "/Tasks",         path);
        Add(items, PermissionNames.TeamsView,         "Nav_Teams",         "/Teams",         path);
        Add(items, PermissionNames.RolesView,         "Nav_Permissions",   "/Roles",         path);
        Add(items, PermissionNames.UsersView,         "Nav_Users",         "/Users",         path);

        return items;
    }

    private void Add(List<NavigationItemViewModel> items, string permission, string labelKey, string url, string path)
    {
        if (!_permissions.HasPermission(permission)) return;

        items.Add(new NavigationItemViewModel
        {
            Label    = _text[labelKey],
            Url      = url,
            IsActive = IsCurrent(url, path)
        });
    }

    private static bool IsCurrent(string url, string path) =>
        string.Equals(path.TrimEnd('/'), url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    private static string InitialOf(string displayName)
    {
        var trimmed = displayName.TrimStart();
        return trimmed.Length == 0 ? string.Empty : trimmed[..1].ToUpperInvariant();
    }
}
