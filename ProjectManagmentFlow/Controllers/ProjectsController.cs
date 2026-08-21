using Microsoft.AspNetCore.Mvc;
using ProjectManagmentFlow.Authorization;
using ProjectManagmentFlow.Filters;

namespace ProjectManagmentFlow.Controllers;

[RequirePermission(PermissionNames.ProjectsView)]
public class ProjectsController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
