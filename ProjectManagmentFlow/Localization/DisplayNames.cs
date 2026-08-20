using Microsoft.Extensions.Localization;

namespace ProjectManagmentFlow;

public static class DisplayNames
{
    public static string Role(IStringLocalizer text, string name, string? nameEn, bool isSystem)
    {
        // الدور المخصّص: البديل الإنجليزيّ إن وُجد، وإلّا الاسم الأصليّ في اللغتين.
        if (!isSystem)
            return DisplayCulture.IsArabic ? name : Fallback(nameEn, name);

        var translated = text[$"Role_{name}"];
        return translated.ResourceNotFound ? name : translated.Value;
    }

    public static string RoleDescription(
        IStringLocalizer text, string name, string description, string? descriptionEn, bool isSystem)
    {
        if (!isSystem)
            return DisplayCulture.IsArabic ? description : Fallback(descriptionEn, description);

        var translated = text[$"Role_{name}_Description"];
        return translated.ResourceNotFound ? description : translated.Value;
    }

    private static string Fallback(string? preferred, string original) =>
        string.IsNullOrWhiteSpace(preferred) ? original : preferred;

    public static string Permission(IStringLocalizer text, string name, string description)
    {
        var translated = text[$"Permission_{name}"];
        return translated.ResourceNotFound ? description : translated.Value;
    }
}
