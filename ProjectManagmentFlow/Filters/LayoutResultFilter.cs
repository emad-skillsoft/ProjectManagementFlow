using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ProjectManagmentFlow.Services.Layout;

namespace ProjectManagmentFlow.Filters;

/// <summary>
/// يحقن نموذج القشرة (LayoutViewModel) في ViewData لكل نتيجة عرض،
/// فيبني كل المتحكمات قشرتها تلقائيًا من LayoutBuilder — صفر تكرار في الإجراءات.
/// </summary>
public sealed class LayoutResultFilter : IAsyncResultFilter
{
    private readonly LayoutBuilder _builder;

    public LayoutResultFilter(LayoutBuilder builder) => _builder = builder;

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Controller is Controller controller && context.Result is ViewResult)
        {
            controller.ViewData["LayoutViewModel"] ??=
                await _builder.BuildAsync(context.HttpContext.RequestAborted);
        }

        await next();
    }
}
