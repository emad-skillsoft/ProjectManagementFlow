using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace ProjectManagmentFlow.Controllers;

/// <summary>
/// تبديل لغة العرض،
/// </summary>
[AllowAnonymous]
public class CultureController : Controller
{
    private static readonly HashSet<string> Supported =
        new(StringComparer.OrdinalIgnoreCase) { "ar-SA", "en-US" };

    /// <summary>
    /// يثبّت اللغة في كوكي الثقافة. IsEssential ضروريّة: بدونها يحجب الإطار
    /// الكوكي متى فُعّلت موافقة الكوكيز، فتعود اللغة إلى الافتراضيّة بعد كلّ طلب.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Set(string culture, string? returnUrl)
    {
        var selected = Supported.Contains(culture) ? culture : "ar-SA";

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(selected)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action("Index", "Home")!);
    }
}
