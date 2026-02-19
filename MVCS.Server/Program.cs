using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MVCS.Server.Data;
using MVCS.Server.Hubs;
using MVCS.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// SQLite + Identity (connection string from config)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured")));

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
});

// Business services (registered via interfaces)
builder.Services.AddSingleton<LogService>();
builder.Services.AddSingleton<ILogService>(sp => sp.GetRequiredService<LogService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<LogService>());

builder.Services.AddSingleton<ISimulatorConnectionService, SimulatorConnectionService>();

// Outbound SignalR client to Simulator's hub (for sending commands)
builder.Services.AddSingleton<ServerHubClient>();
builder.Services.AddSingleton<IServerHubClient>(sp => sp.GetRequiredService<ServerHubClient>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ServerHubClient>());

// Data retention background service
builder.Services.AddHostedService<DataRetentionService>();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database");

var app = builder.Build();

// Auto-migrate database & seed admin
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    // Seed admin user (credentials from config)
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var adminEmail = builder.Configuration["SeedAdmin:Email"] ?? "admin@mvcs.com";
    var adminPassword = builder.Configuration["SeedAdmin:Password"] ?? "Admin123";

    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var admin = new IdentityUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(admin, adminPassword);
    }
}

// Global exception handling
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exceptionFeature?.Error,
            "Unhandled exception at {Path}", exceptionFeature?.Path);

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
    });
});

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<VesselHub>("/vesselhub");
app.MapHealthChecks("/health");

app.Run();
