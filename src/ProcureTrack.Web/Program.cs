using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProcureTrack.Web.Data;
using ProcureTrack.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services ----------

builder.Services.AddControllersWithViews(options =>
{
    // Require a valid antiforgery token on every POST, PUT, PATCH, and DELETE in the app.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddDbContext<ProcureTrackDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ProcureTrack")
        ?? throw new InvalidOperationException("Connection string 'ProcureTrack' was not found.")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ApprovalWorkflow>();

// Demo cookie authentication. See README: production would use Windows Authentication / Active Directory.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "ProcureTrack.Auth";
        options.Cookie.HttpOnly = true;                           // not readable by JavaScript (limits XSS impact)
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;  // only sent over HTTPS
        options.Cookie.SameSite = SameSiteMode.Lax;               // not sent on cross-site POSTs
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    // Deny by default: every endpoint requires a signed-in user unless marked [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

// Development convenience: create or upgrade the database and load sample data on startup.
// Production would apply reviewed migration scripts instead (see README).
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ProcureTrackDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

// ---------- HTTP pipeline (order matters, like Express middleware) ----------

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Single-locale internal app: format currency and dates as en-US regardless of the server's regional settings.
app.UseRequestLocalization("en-US");

app.UseRouting();

app.UseAuthentication();   // who are you? (reads the cookie)
app.UseAuthorization();    // are you allowed? (applies [Authorize] and the fallback policy)

// CSS, JavaScript, and images must load on the sign-in page, before anyone is signed in.
app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();