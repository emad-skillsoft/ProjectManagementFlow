using Microsoft.Extensions.Localization;

namespace ProjectManagmentFlow.ViewModels;

public sealed class LayoutViewModel
{
    public string MetaDescription { get; set; } = string.Empty;

    public AppHeaderViewModel Header { get; set; } = new();

    public AppSidebarViewModel Sidebar { get; set; } = new();

    public AppBreadcrumbViewModel? Breadcrumb { get; set; }

    public AppFooterViewModel Footer { get; set; } = new();

    public bool ShowFooter { get; set; } = true;

    public static LayoutViewModel CreateDefault(IStringLocalizer text)
    {
        var isArabic = DisplayCulture.IsArabic;

        // الشريط الجانبيّ يبنيه LayoutBuilder حسب صلاحيّات المستخدم؛
        // هنا نتولّد بدون أقسام ليُملأ لاحقًا إن أمكن.
        var sidebar = new AppSidebarViewModel
        {
            IsVisible = false,
            Title = text["App_Sidebar"]
        };

        return new LayoutViewModel
        {
            MetaDescription = text["App_MetaDescription"],
            Header = new AppHeaderViewModel
            {
                Brand = new AppBrandViewModel
                {
                    Name = text["App_BrandName"],
                    Descriptor = text["App_MetaDescription"],
                    HomeUrl = "/",
                    // المشروع الهدف لا يملك ملفّ الشعار؛ الثيم يسقط تلقائيًا
                    // إلى العلامت النصّيّة FallbackMark.
                    LogoUrl = null,
                    LogoAlt = text["App_BrandLogoAlt"],
                    FallbackMark = text["App_BrandMark"]
                },
                NavigationLabel = text["App_MainNavigation"],
                MobileMenuLabel = text["App_OpenMenu"],
                SidebarMenuLabel = text["App_OpenSidebar"],
                SidebarTargetId = sidebar.Id,
                // القائمة الرئيسيّة فارغة بالافتراض؛ عناصر التنقّل تبنيها
                // الخدمات حسب صلاحيّات المستخدم (الشريط الجانبيّ).
                Items = [],
                LanguageSwitch = new LanguageSwitchViewModel
                {
                    Controller = "Culture",
                    Action = "Set",
                    // اسم اللغة الأخرى قرار عرض تنفّذه الواجهة بلغتها هي
                    // («English» في وضع العربيّة و«العربية» في وضع الإنجليزيّ)،
                    // فيبقى فارغاً هنا فلا يتسلّل نصّ مثبّت إلى كود C#.
                    TargetCulture = isArabic ? "en-US" : "ar-SA",
                    Label = string.Empty,
                    LanguageCode = isArabic ? "en" : "ar"
                }
            },
            Sidebar = sidebar,
            Footer = AppFooterViewModel.CreateDefault(text)
        };
    }
}

public sealed class AppHeaderViewModel
{
    public string? CssClass { get; set; }

    public AppBrandViewModel Brand { get; set; } = new();

    public string NavigationLabel { get; set; } = "Main navigation";

    public string MobileMenuLabel { get; set; } = "Open navigation";

    public string SidebarMenuLabel { get; set; } = "Open sidebar";

    public string SidebarTargetId { get; set; } = "app-sidebar";

    public string? ContextBadge { get; set; }

    public IReadOnlyList<NavigationItemViewModel> Items { get; set; } = [];

    public HeaderUserViewModel? User { get; set; }

    public LanguageSwitchViewModel? LanguageSwitch { get; set; }
}

public sealed class AppBrandViewModel
{
    public string Name { get; set; } = string.Empty;

    public string? Descriptor { get; set; }

    public string HomeUrl { get; set; } = "/";

    public string? LogoUrl { get; set; }

    public string LogoAlt { get; set; } = "Entity logo";

    public string? SecondaryLogoUrl { get; set; }

    public string? SecondaryLogoAlt { get; set; }

    public string FallbackMark { get; set; } = "DS";
}

public sealed class NavigationItemViewModel
{
    public string Label { get; set; } = string.Empty;

    public string Url { get; set; } = "#";

    public bool IsActive { get; set; }

    public bool IsDisabled { get; set; }

    public string? Badge { get; set; }
}

public sealed class HeaderUserViewModel
{
    public string DisplayName { get; set; } = string.Empty;

    public string? Role { get; set; }

    public string ProfileUrl { get; set; } = "#";

    public string Initials { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }
}

public sealed class LanguageSwitchViewModel
{
    public string Controller { get; set; } = "Culture";

    public string Action { get; set; } = "Set";

    public string TargetCulture { get; set; } = "ar-SA";

    public string Label { get; set; } = string.Empty;

    public string LanguageCode { get; set; } = "ar";
}

public sealed class AppSidebarViewModel
{
    public bool IsVisible { get; set; }

    public string Id { get; set; } = "app-sidebar";

    public string Title { get; set; } = "Sidebar";

    public IReadOnlyList<SidebarSectionViewModel> Sections { get; set; } = [];
}

public sealed class AppBreadcrumbViewModel
{
    public string Label { get; set; } = "Breadcrumb";

    public string Separator { get; set; } = "›";

    public IReadOnlyList<AppBreadcrumbItemViewModel> Items { get; set; } = [];

    public IReadOnlyList<AppBreadcrumbItemViewModel> GetDisplayItems()
    {
        if (Items.Count <= 4 || Items.Any(item => item.IsFolded))
        {
            return Items;
        }

        var hiddenItems = Items.Skip(1).Take(Items.Count - 3).ToArray();
        var hiddenLabels = string.Join(Separator, hiddenItems.Select(item => item.Label));
        var folded = new AppBreadcrumbItemViewModel
        {
            Label = "…",
            Url = hiddenItems.LastOrDefault()?.Url,
            Title = hiddenLabels,
            AccessibleLabel = $"Collapsed ancestors: {string.Join(", ", hiddenItems.Select(item => item.Label))}",
            IsFolded = true
        };

        return [Items[0], folded, Items[^2], Items[^1]];
    }
}

public sealed class AppBreadcrumbItemViewModel
{
    public string Label { get; set; } = string.Empty;

    public string? Url { get; set; }

    public string? Title { get; set; }

    public string? AccessibleLabel { get; set; }

    public bool IsFolded { get; set; }

    public bool IsCurrent { get; set; }
}

public sealed class SidebarSectionViewModel
{
    public string? Label { get; set; }

    public IReadOnlyList<NavigationItemViewModel> Items { get; set; } = [];
}

public sealed class AppFooterViewModel
{
    public string NavigationLabel { get; set; } = "Footer navigation";

    public IReadOnlyList<FooterLinkViewModel> Links { get; set; } = [];

    public FooterLinkViewModel GovernmentPortal { get; set; } = new();

    public string LastUpdatedText { get; set; } = string.Empty;

    public string CopyrightText { get; set; } = string.Empty;

    public static AppFooterViewModel CreateDefault(IStringLocalizer text) => new()
    {
        NavigationLabel = text["Footer_Navigation"],
        Links =
        [
            new() { Label = text["Footer_Privacy"], Url = "/privacy" },
            new() { Label = text["Footer_Terms"], Url = "/terms" },
            new() { Label = text["Footer_Contact"], Url = "/contact" },
            new() { Label = text["Footer_Faq"], Url = "/faq" },
            new() { Label = text["Footer_Sitemap"], Url = "/sitemap" },
            new() { Label = text["Footer_Accessibility"], Url = "/accessibility" }
        ],
        GovernmentPortal = new FooterLinkViewModel
        {
            Label = text["Footer_UnifiedPlatform"],
            Url = "https://my.gov.sa",
            IsExternal = true
        },
        LastUpdatedText = text["Footer_LastUpdated"],
        CopyrightText = text["Footer_Copyright"]
    };
}

public sealed class FooterLinkViewModel
{
    public string Label { get; set; } = string.Empty;

    public string Url { get; set; } = "#";

    public bool IsExternal { get; set; }
}
