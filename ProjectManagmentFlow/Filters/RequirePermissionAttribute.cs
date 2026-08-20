using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ProjectManagmentFlow.Services.Security;
using ProjectManagmentFlow.Services.Users;

namespace ProjectManagmentFlow.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute : TypeFilterAttribute
{
    public RequirePermissionAttribute(string permission) : base(typeof(PermissionFilter))
    {
        Arguments = new object[] { permission };
    }
}

public class PermissionFilter : IAsyncActionFilter
{
    private readonly string _permission;

    public PermissionFilter(string permission) => _permission = permission;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // احترام [AllowAnonymous] الموضوع على الإجراء أو على المتحكّم.
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            await next();
            return;
        }

        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            // ChallengeResult يوجّه إلى LoginPath ويحمل معه returnUrl تلقائياً.
            context.Result = new ChallengeResult();
            return;
        }

        if (!user.HasClaim(UserPrincipalFactory.PermissionClaimType, _permission))
        {
            // ForbidResult يوجّه إلى AccessDeniedPath برمز حالة 403 بدل صفحة برمز 200.
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
