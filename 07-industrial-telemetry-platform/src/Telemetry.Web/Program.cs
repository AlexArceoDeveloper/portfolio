using Telemetry.Web;
using Telemetry.Web.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddProblemDetails();

var dataPath = builder.Configuration["Telemetry:DataPath"];
if (string.IsNullOrWhiteSpace(dataPath))
{
    builder.Services.AddSingleton<ITelemetryStore, InMemoryTelemetryStore>();
}
else
{
    builder.Services.AddSingleton<ITelemetryStore>(
        _ => new JsonLineTelemetryStore(dataPath));
}

var mqttEnabled = builder.Configuration.GetValue("Mqtt:Enabled", false);
if (mqttEnabled)
{
    builder.Services.AddSingleton(new MqttOptions(
        builder.Configuration["Mqtt:Host"] ?? "localhost",
        builder.Configuration.GetValue("Mqtt:Port", 1883),
        builder.Configuration["Mqtt:TopicPrefix"] ?? "portfolio/telemetry",
        builder.Configuration["Mqtt:ClientId"] ?? $"telemetry-web-{Environment.MachineName}"));
    builder.Services.AddSingleton<MqttTelemetryBridge>();
    builder.Services.AddSingleton<ITelemetryPublisher>(
        services => services.GetRequiredService<MqttTelemetryBridge>());
    builder.Services.AddHostedService(
        services => services.GetRequiredService<MqttTelemetryBridge>());
}
else
{
    builder.Services.AddSingleton<ITelemetryPublisher, LocalTelemetryPublisher>();
}
builder.Services.AddSingleton<TelemetrySimulator>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapGet("/health", () => TypedResults.Ok(new
{
    status = "healthy",
    mqtt = mqttEnabled ? "enabled" : "local-loopback",
    persistence = string.IsNullOrWhiteSpace(dataPath) ? "in-memory" : "json-lines"
}));
app.MapGet("/api/telemetry", async (
    ITelemetryStore store,
    int? limit,
    CancellationToken cancellationToken) =>
    TypedResults.Ok(await store.LatestAsync(
        Math.Clamp(limit ?? 50, 1, 500),
        cancellationToken)));
app.MapPost("/api/telemetry/simulate/{deviceId}", async (
    string deviceId,
    TelemetrySimulator simulator,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(deviceId))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(deviceId)] = ["Device ID is required."]
        });
    }

    var reading = await simulator.PublishAsync(deviceId, cancellationToken);
    return Results.Accepted($"/api/telemetry?deviceId={Uri.EscapeDataString(deviceId)}", reading);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.Run();

public partial class Program;
