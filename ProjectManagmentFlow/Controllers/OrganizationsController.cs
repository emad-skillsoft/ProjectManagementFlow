using Microsoft.AspNetCore.Mvc;
using ProjectManagmentFlow.Authorization;
using ProjectManagmentFlow.Filters;

namespace ProjectManagmentFlow.Controllers;

[RequirePermission(PermissionNames.OrganizationsView)]
public class OrganizationsController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
