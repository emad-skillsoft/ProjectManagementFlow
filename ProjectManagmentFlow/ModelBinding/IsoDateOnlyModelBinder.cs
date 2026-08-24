using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ProjectManagmentFlow.ModelBinding;

/// <summary>
/// حقول HTML من النوع date ترسل yyyy-MM-dd بصرف النظر عن لغة العرض؛
/// لذلك يجب ألا تُفسَّر بتقويم ar-SA الهجري.
/// </summary>
public sealed class IsoDateOnlyModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueResult == ValueProviderResult.None)
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);
        var value = valueResult.FirstValue;

        if (string.IsNullOrWhiteSpace(value))
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        if (DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date))
        {
            bindingContext.Result = ModelBindingResult.Success(date);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Invalid ISO date.");
        return Task.CompletedTask;
    }
}
