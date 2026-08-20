using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectManagmentFlow.Data;
using ProjectManagmentFlow.Models;
using ProjectManagmentFlow.Services.Roles;
using ProjectManagmentFlow.Services.Security;
using ProjectManagmentFlow.Services.Users;
using ProjectManagmentFlow.Services.Permissions;

//Added By Emad El Faramawi
var builder = WebApplication.CreateBuilder(args);
//Added By omar alsulami

// Add services to the container.
builder.Services.AddControllersWithViews();
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();


app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();



app.Run();
