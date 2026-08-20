using System.Globalization;

namespace ProjectManagmentFlow;

/// <summary>
/// سؤال «ما لغة العرض؟» في موضع واحد يسأله كلّ من احتاج الجواب.
///
/// وهو سؤال يقلّ الاحتياج إليه كلّما اتّسعت الترجمة: النصّ يأتي من
/// IStringLocalizer، فلا يبقى لهذه الخاصّية إلّا ما يعتمد على اللغة بنيوياً
/// — اتّجاه الصفحة، وزرّ تبديل اللغة، وتصريف الأعداد العربيّ.
/// </summary>
public static class DisplayCulture
{
    public static bool IsArabic =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";
}
