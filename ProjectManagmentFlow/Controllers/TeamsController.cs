using Microsoft.AspNetCore.Mvc;
using ProjectManagmentFlow.Authorization;
using ProjectManagmentFlow.Filters;

namespace ProjectManagmentFlow.Controllers;

[RequirePermission(PermissionNames.TeamsView)]
public class TeamsController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
