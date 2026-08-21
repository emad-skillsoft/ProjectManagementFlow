using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagmentFlow.Models;

namespace ProjectManagmentFlow.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        // الصفحة الرئيسيّة هي لوحة معلومات؛ كل ما عداه خلف صلاحيّات معيّنة.
        return RedirectToAction("Index", "Dashboard");
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
