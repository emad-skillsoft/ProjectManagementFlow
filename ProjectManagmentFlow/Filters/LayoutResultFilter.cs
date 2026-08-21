using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ProjectManagmentFlow.Services.Layout;

namespace ProjectManagmentFlow.Filters;

/// <summary>
/// يحقن نموذج القشرة (LayoutViewModel) في ViewData لكل نتيجة عرض،
/// فيبني كل المتحكمات قشرتها تلقائيًا من LayoutBuilder — صفر تكرار في الإجراءات.
/// </summary>
public sealed class LayoutResultFilter : IResultFilter
{
    private readonly LayoutBuilder _builder;

    public LayoutResultFilter(LayoutBuilder builder) => _builder = builder;

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Controller is Controller controller && context.Result is ViewResult)
        {
            controller.ViewData["LayoutViewModel"] ??= _builder.Build();
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
        // لا حاجة للمعالجة بعد التنفيذ.
    }
}
