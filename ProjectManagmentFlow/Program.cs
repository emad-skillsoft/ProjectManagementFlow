using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using ProjectManagmentFlow;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Localization;
using ProjectManagmentFlow.ModelBinding;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Filters;
using ProjectManagmentFlow.Services.Layout;
using ProjectManagmentFlow.Services.Roles;
using ProjectManagmentFlow.Services.Security;
using ProjectManagmentFlow.Services.Users;
using ProjectManagmentFlow.Services.Organizations;
using ProjectManagmentFlow.Services.Permissions;
using ProjectManagmentFlow.Services.Projects;
using ProjectManagmentFlow.Services.Activity;
using ProjectManagmentFlow.Services.Teams;
using ProjectManagmentFlow.Services.Tasks;

//Added By Emad El Faramawi
var builder = WebApplication.CreateBuilder(args);
//Added By omar alsulami

// Add services to the container.
// الترجمات من Resources/{culture}.json بدل ملفّات .resx
builder.Services.AddLocalization();
builder.Services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<LayoutResultFilter>();
})
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(Messages)));

// رسائل ربط النماذج المترجَمة — عبر IConfigureOptions حتّى تُحقن المُوطِّن بعد بناء الحاوية.
builder.Services.AddSingleton<IConfigureOptions<MvcOptions>, LocalizedModelBindingMessages>();
// 
builder.Services.AddWebEncoders(options =>
{
    options.TextEncoderSettings = new TextEncoderSettings(
        UnicodeRanges.BasicLatin,
        UnicodeRanges.Arabic);
});

// فترة إعادة التحقّق من الكوكي مقابل القاعدة — قابلة للضبط لتيسير الاختبار.
var revalidationInterval = TimeSpan.FromMinutes(
    builder.Configuration.GetValue("Auth:RevalidationIntervalMinutes",
        PrincipalRevalidator.DefaultValidationInterval.TotalMinutes));

// 1. إعداد DbContext MSSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. إعداد Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ProjectManagmentFlow.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ReturnUrlParameter = "returnUrl";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        // إعادة التحقّق من الحساب والصلاحيات مقابل DB
        options.Events.OnValidatePrincipal = context =>
            PrincipalRevalidator.ValidateAsync(context, revalidationInterval);
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddHttpContextAccessor();

// security  servic
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IUserPrincipalFactory, UserPrincipalFactory>();
builder.Services.AddScoped<ISecurityStampService, SecurityStampService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// role and permission
builder.Services.AddScoped<IRoleQueryService, RoleQueryService>();
builder.Services.AddScoped<IRoleCommandService, RoleCommandService>();
builder.Services.AddScoped<IUserRoleQueryService, UserRoleQueryService>();
builder.Services.AddScoped<IUserRoleCommandService, UserRoleCommandService>();
builder.Services.AddScoped<IPermissionCatalog, PermissionCatalog>();
builder.Services.AddScoped<IUserQueryService, UserQueryService>();

// organizations
builder.Services.AddScoped<IOrganizationQueryService, OrganizationQueryService>();
builder.Services.AddScoped<IOrganizationCommandService, OrganizationCommandService>();
builder.Services.AddScoped<IOrganizationMemberQueryService, OrganizationMemberQueryService>();
builder.Services.AddScoped<IOrganizationMemberCommandService, OrganizationMemberCommandService>();
builder.Services.AddScoped<IOrgWorkspaceService, OrgWorkspaceService>();

// projects
builder.Services.AddScoped<IProjectQueryService, ProjectQueryService>();
builder.Services.AddScoped<IProjectCommandService, ProjectCommandService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<ITeamQueryService, TeamQueryService>();
builder.Services.AddScoped<ITeamCommandService, TeamCommandService>();
builder.Services.AddScoped<ITaskQueryService, TaskQueryService>();
builder.Services.AddScoped<ITaskCommandService, TaskCommandService>();


// يُحقنها LayoutResultFilter في ViewData.
builder.Services.AddScoped<LayoutBuilder>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// اللغات التي تدعمها المنصه 
var supportedCultures = new[] { new CultureInfo("ar-SA"), new CultureInfo("en-US") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("ar-SA"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
    RequestCultureProviders =
    {
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    }
});

app.UseRouting();


app.UseAuthentication();
app.UseAuthorization();

//Assets  تستثنيها من FallbackPolicy
// ⚠ MapStaticAssets يقدّم النسخ المضغوطة المولَّدة عند النشر وحده. تشغيل التطبيق
// من bin في بيئةٍ غير Development يردّ ٢٠٠ بجسمٍ فارغ لكلّ متصفّحٍ يطلب br/gzip —
// أي صفحةٌ بلا CSS ولا JS. النشر بـdotnet publish يولّدها فيزول العطل؛ فلا تُشغَّل
// بيئة الإنتاج من bin مباشرةً.
app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    await DbInitializer.SeedAsync(context, passwordHasher);
}

app.Run();
