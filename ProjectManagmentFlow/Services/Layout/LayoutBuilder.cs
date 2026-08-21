using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.Authorization;
using ProjectManagmentFlow.Services.Permissions;
using ProjectManagmentFlow.ViewModels;

namespace ProjectManagmentFlow.Services.Layout;

/// <summary>
/// يبني نموذج القشرة لكل طلب: يملأ ترويسة المستخدم من ادّعاءات المصادقة،
/// وشريطًا جانبيًّا يعرض فقط ما يملكه المستخدم صلاحية رؤيته (لا قائمة ثابتة).
/// يُحقن في المتحكّمات عبر LayoutResultFilter فلا يُكتب هذا الكود في كلّ إجراء.
/// </summary>
public class LayoutBuilder
{
    private readonly IPermissionService _permissions;
    private readonly IStringLocalizer<Messages> _text;
    private readonly IHttpContextAccessor _http;

    public LayoutBuilder(
        IPermissionService permissions,
        IStringLocalizer<Messages> text,
        IHttpContextAccessor http)
    {
        _permissions = permissions;
        _text = text;
        _http = http;
    }

    public LayoutViewModel Build()
    {
        var model = LayoutViewModel.CreateDefault(_text);
        var user = _http.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            // زائر غير مُصادَق: لا اسم مستخدم في الترويسة ولا شريط جانبيّ —
            // التنقّل الداخليّ حقّ للمُصادَقين وحدهم.
            return model;
        }

        // مستخدم الترويسة — من ادّعاءات المصادقة (اسم العرض = FullName/Email).
        model.Header.User = new HeaderUserViewModel
        {
            DisplayName = user.Identity.Name ?? string.Empty,
            ProfileUrl  = "#"
        };

        var path = _http.HttpContext?.Request?.Path.Value ?? string.Empty;
        model.Sidebar.Sections = BuildSidebarSections(path);
        model.Sidebar.IsVisible = model.Sidebar.Sections.Any(s => s.Items.Count > 0);

        return model;
    }

    private List<SidebarSectionViewModel> BuildSidebarSections(string path)
    {
        var items = new List<NavigationItemViewModel>();

        // «الرئيسيّة» ليست خلف صلاحية — المصادقة وحدها تكفي لدخولها،
        // فتظهر أعلى الشريط لكلّ مُصادَق.
        items.Add(new NavigationItemViewModel
        {
            Label    = _text["Dashboard_Title"],
            Url      = "/Dashboard",
            IsActive = IsCurrent("/Dashboard", path)
        });

        // عناصر أخرى تظهر فقط لمن يملك صلاحية العرض.
        Add(PermissionNames.OrganizationsView, "Nav_Organizations", "/Organizations");
        Add(PermissionNames.ProjectsView,      "Nav_Projects",      "/Projects");
        Add(PermissionNames.TasksView,         "Nav_MyTasks",       "/Tasks");
        Add(PermissionNames.TeamsView,         "Nav_Teams",         "/Teams");
        Add(PermissionNames.RolesView,         "Nav_Roles",         "/Roles");
        Add(PermissionNames.UsersView,         "Nav_Users",         "/Users");

        return [ new SidebarSectionViewModel { Items = items } ];

        void Add(string permission, string labelKey, string url)
        {
            if (!_permissions.HasPermission(permission)) return;

            items.Add(new NavigationItemViewModel
            {
                Label    = _text[labelKey],
                Url      = url,
                IsActive = IsCurrent(url, path)
            });
        }
    }

    private static bool IsCurrent(string url, string path) =>
        string.Equals(path.TrimEnd('/'), url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
}
