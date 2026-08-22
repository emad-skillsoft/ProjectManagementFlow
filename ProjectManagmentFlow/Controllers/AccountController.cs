using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ProjectManagmentFlow.ViewModels;
using ProjectManagmentFlow.Services.Layout;
using ProjectManagmentFlow.Services.Security;

namespace ProjectManagmentFlow.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly IStringLocalizer<Messages> _text;
    private readonly LayoutBuilder _layout;

    public AccountController(IAuthService authService, IStringLocalizer<Messages> text, LayoutBuilder layout)
    {
        _authService = authService;
        _text = text;
        _layout = layout;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return SignInView(new AccountLoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(AccountLoginViewModel viewModel, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return SignInView(viewModel);

        var result = await _authService.LoginAsync(viewModel.Email, viewModel.Password, cancellationToken);

        if (result != LoginResult.Success)
        {
            ModelState.AddModelError(string.Empty, DescribeFailure(result));
            return SignInView(viewModel);
        }

        return RedirectToLocal(viewModel.ReturnUrl);
    }

    private IActionResult SignInView(AccountLoginViewModel viewModel)
    {
        ViewData["LayoutViewModel"] = _layout.BuildForSignIn();
        return View("Login", viewModel);
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

    private string DescribeFailure(LoginResult result) => result switch
    {
        LoginResult.Disabled  => _text["Login_Disabled"],
        LoginResult.LockedOut => _text["Login_LockedOut", AuthService.LockoutDuration.TotalMinutes.ToString("0")],
        _                     => _text["Login_InvalidCredentials"]
    };

    /// <summary>
    /// لا يُعاد التوجيه إلّا لمسار داخليّ؛ عنوان خارجيّ في returnUrl يفتح ثغرة إعادة توجيه مفتوحة.
    /// </summary>
    private IActionResult RedirectToLocal(string? returnUrl)
        => Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("Index", "Home");
}
    