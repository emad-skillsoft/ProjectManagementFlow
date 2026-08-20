using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace ProjectManagmentFlow.ModelBinding;

/// <summary>
/// رسائل ربط النماذج الافتراضيّة إنجليزيّة دائماً مهما كانت ثقافة الطلب،
/// فتظهر «The value 'x' is not valid» داخل واجهة عربيّة. هذا يستبدلها بنصوص مترجَمة.
///
/// يُنفَّذ عبر IConfigureOptions لا داخل AddControllersWithViews مباشرةً: اللامدا هناك
/// تعمل وقت بناء الحاوية فلا مُوطِّن يُحقن فيها، أمّا IConfigureOptions فيُحلّ بعد بنائها.
/// والمُسنِدات نفسها دوالّ مؤجَّلة تُستدعى مع كلّ طلب، فتقرأ ثقافته الجارية.
/// </summary>
public sealed class LocalizedModelBindingMessages(IStringLocalizer<Messages> text)
    : IConfigureOptions<MvcOptions>
{
    public void Configure(MvcOptions options)
    {
        var provider = options.ModelBindingMessageProvider;

        provider.SetValueIsInvalidAccessor(value => text["Binding_ValueIsInvalid", value]);
        provider.SetValueMustNotBeNullAccessor(_ => text["Binding_ValueMustNotBeNull"]);
        provider.SetMissingBindRequiredValueAccessor(field => text["Binding_MissingBindRequired", field]);
        provider.SetMissingKeyOrValueAccessor(() => text["Binding_MissingKeyOrValue"]);
        provider.SetMissingRequestBodyRequiredValueAccessor(() => text["Binding_MissingRequestBody"]);
        provider.SetAttemptedValueIsInvalidAccessor((value, field) => text["Binding_AttemptedValueIsInvalid", value, field]);
        provider.SetNonPropertyAttemptedValueIsInvalidAccessor(value => text["Binding_ValueIsInvalid", value]);
        provider.SetUnknownValueIsInvalidAccessor(field => text["Binding_UnknownValueIsInvalid", field]);
        provider.SetNonPropertyUnknownValueIsInvalidAccessor(() => text["Binding_NonPropertyUnknownValueIsInvalid"]);
        provider.SetValueMustBeANumberAccessor(field => text["Binding_ValueMustBeANumber", field]);
        provider.SetNonPropertyValueMustBeANumberAccessor(() => text["Binding_NonPropertyValueMustBeANumber"]);
    }
}
