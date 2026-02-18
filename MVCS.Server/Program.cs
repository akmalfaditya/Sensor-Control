using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MVCS.Server.Data;
using MVCS.Server.Hubs;
using MVCS.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for HTTP on port 5000
builder.WebHost.UseUrls("http://localhost:5000");

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// SQLite + Identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=mvcs.db"));

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

// Business services
builder.Services.AddScoped<LogService>();
builder.Services.AddSingleton<SimulatorConnectionService>();

// Outbound SignalR client to Simulator's hub (for sending commands)
builder.Services.AddSingleton<ServerHubClient>();
builder.Services.AddHostedService<ServerHubClient>(sp => sp.GetRequiredService<ServerHubClient>());

var app = builder.Build();

// Auto-migrate database & seed admin
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    // Seed admin user
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    const string adminEmail = "admin@mvcs.com";
    const string adminPassword = "Admin123";

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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<VesselHub>("/vesselhub");

app.Run();
