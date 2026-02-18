using MVCS.Simulator.Services;
using MVCS.Simulator.Workers;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for HTTP on port 5100
builder.WebHost.UseUrls("http://localhost:5100");

// Add controllers + SignalR server
builder.Services.AddControllers();
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

app.MapControllers();
app.MapHub<MVCS.Simulator.Hubs.SimulatorHub>("/simulatorhub");

app.Run();
