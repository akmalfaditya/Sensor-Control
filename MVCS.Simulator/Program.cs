using Microsoft.AspNetCore.Diagnostics;
using MVCS.Simulator.Hubs;
using MVCS.Simulator.Services;
using MVCS.Simulator.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add MVC + Controllers + SignalR server
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MVCS Simulator API", Version = "v1" });
});

// Register services (via interfaces)
builder.Services.AddSingleton<SimulationStateService>();
builder.Services.AddSingleton<ISimulationStateService>(sp => sp.GetRequiredService<SimulationStateService>());

builder.Services.AddSingleton<SimulatorHubClient>();
builder.Services.AddSingleton<ISimulatorHubClient>(sp => sp.GetRequiredService<SimulatorHubClient>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<SimulatorHubClient>());

// Register background workers
builder.Services.AddHostedService<CompassBroadcaster>();
builder.Services.AddHostedService<WaterBroadcaster>();

var app = builder.Build();

// Always enable Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MVCS Simulator v1");
    c.RoutePrefix = "swagger";
});

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

// MVC routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=HomeView}/{action=Index}/{id?}");

// API controllers
app.MapControllers();

// SignalR hubs
app.MapHub<SimulatorHub>("/simulatorhub");
app.MapHub<SimulatorDashboardHub>("/simulatordashboardhub");

app.Run();
