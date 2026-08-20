using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagmentFlow.ViewModels;
using ProjectManagmentFlow.Services.Security;

namespace ProjectManagmentFlow.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IAuthService _authService;

    public AccountController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new AccountLoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(AccountLoginViewModel viewModel, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(viewModel);

        var result = await _authService.LoginAsync(viewModel.Email, viewModel.Password, cancellationToken);

        if (result != LoginResult.Success)
        {
            ModelState.AddModelError(string.Empty, DescribeFailure(result));
            return View(viewModel);
        }

        return RedirectToLocal(viewModel.ReturnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("~/Views/Shared/AccessDenied.cshtml");
    }

    private static string DescribeFailure(LoginResult result) => result switch
    {
        LoginResult.Disabled => "هذا الحساب غير مفعّل. راجع مدير النظام.",
        LoginResult.LockedOut =>
            $"تم إيقاف الحساب مؤقّتاً بعد تكرار المحاولات الفاشلة. أعد المحاولة بعد {AuthService.LockoutDuration.TotalMinutes:0} دقائق.",
        _ => "بيانات الدخول غير صحيحة."
    };

    /// <summary>
    /// لا يُعاد التوجيه إلّا لمسار داخليّ؛ عنوان خارجيّ في returnUrl يفتح ثغرة إعادة توجيه مفتوحة.
    /// </summary>
    private IActionResult RedirectToLocal(string? returnUrl)
        => Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("Index", "Home");
}
    