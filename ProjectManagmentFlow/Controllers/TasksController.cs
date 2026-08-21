using Microsoft.AspNetCore.Mvc;
using ProjectManagmentFlow.Authorization;
using ProjectManagmentFlow.Filters;

namespace ProjectManagmentFlow.Controllers;

[RequirePermission(PermissionNames.TasksView)]
public class TasksController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
