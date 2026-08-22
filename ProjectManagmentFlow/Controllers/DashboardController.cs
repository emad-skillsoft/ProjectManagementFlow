using Microsoft.AspNetCore.Mvc;

namespace ProjectManagmentFlow.Controllers;

public class DashboardController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
