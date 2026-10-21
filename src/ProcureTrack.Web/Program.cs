using Microsoft.EntityFrameworkCore;
using ProcureTrack.Web.Data;
using ProcureTrack.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ProcureTrackDbContext>(options => 
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ProcureTrack")
        ?? throw new InvalidOperationException("Connection string 'ProcureTrack' was not found.")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ApprovalWorkflow>();
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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
// Single-locale internal app: format currency and dates as en-US regardless of the server's regional settings.
app.UseRequestLocalization("en-US");
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
