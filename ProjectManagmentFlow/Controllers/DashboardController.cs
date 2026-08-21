using Microsoft.AspNetCore.Mvc;

namespace ProjectManagmentFlow.Controllers;

public class DashboardController : Controller
{
    // اللوحة الرئيسيّة: المصادقة وحدها تكفي، دون صلاحية معيّنة.
    public IActionResult Index()
    {
        return View();
    }
}
