using MVCS.Simulator.Hubs;
using MVCS.Simulator.Services;
using MVCS.Simulator.Workers;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for HTTP on port 5100
builder.WebHost.UseUrls("http://localhost:5100");

// Add MVC + Controllers + SignalR server
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MVCS Simulator API", Version = "v1" });
});

// Register services
builder.Services.AddSingleton<SimulationStateService>();
builder.Services.AddSingleton<SimulatorHubClient>();
builder.Services.AddHostedService<SimulatorHubClient>(sp => sp.GetRequiredService<SimulatorHubClient>());

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
