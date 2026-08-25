using Commerce.Api;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IRiskAssessor, ExplainableRiskAssessor>();
builder.Services.AddSingleton<IPaymentGateway, SandboxPaymentGateway>();
var postgresConnection = builder.Configuration.GetConnectionString("Commerce");
if (string.IsNullOrWhiteSpace(postgresConnection))
{
    builder.Services.AddSingleton<IPaymentStore, InMemoryPaymentStore>();
}
else
{
    builder.Services.AddPooledDbContextFactory<CommerceDbContext>(options =>
        options.UseNpgsql(postgresConnection));
    builder.Services.AddSingleton<IPaymentStore, PostgresPaymentStore>();
}
builder.Services.AddSingleton<PaymentService>();
builder.Services.AddSingleton(new WebhookSignatureVerifier(
    builder.Configuration["Payments:WebhookSecret"] ?? "development-only-secret"));

var telemetry = builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("commerce-api"))
    .WithTracing(tracing => tracing
        .AddSource(CommerceTelemetry.ActivitySourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddMeter(CommerceTelemetry.MeterName)
        .AddAspNetCoreInstrumentation());

if (Uri.TryCreate(
    builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"],
    UriKind.Absolute,
    out var telemetryEndpoint))
{
    telemetry
        .WithTracing(tracing => tracing.AddOtlpExporter(options =>
            options.Endpoint = telemetryEndpoint))
        .WithMetrics(metrics => metrics.AddOtlpExporter(options =>
            options.Endpoint = telemetryEndpoint));
}

var app = builder.Build();
app.UseExceptionHandler();

if (!string.IsNullOrWhiteSpace(postgresConnection))
{
    await using var scope = app.Services.CreateAsyncScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CommerceDbContext>>();
    await using var database = await factory.CreateDbContextAsync();
    await database.Database.EnsureCreatedAsync();
}

app.MapGet("/health", () => TypedResults.Ok(new
{
    status = "healthy",
    persistence = string.IsNullOrWhiteSpace(postgresConnection) ? "in-memory" : "postgresql"
}));

app.MapPost("/api/payments", async (
    HttpRequest httpRequest,
    CreatePaymentCommand command,
    PaymentService service,
    CancellationToken cancellationToken) =>
{
    if (!httpRequest.Headers.TryGetValue("Idempotency-Key", out var key) ||
        string.IsNullOrWhiteSpace(key))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Idempotency-Key"] = ["A non-empty Idempotency-Key header is required."]
        });
    }

    if (string.IsNullOrWhiteSpace(command.OrderId) ||
        command.Amount <= 0 ||
        command.Currency.Length != 3)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(command)] = ["OrderId, a positive amount and a three-letter currency are required."]
        });
    }

    var (result, created) = await service.CreateAsync(key.ToString(), command, cancellationToken);
    return created
        ? Results.Created($"/api/payments/{result.PaymentId}", result)
        : Results.Ok(result);
});

app.MapPost("/api/payments/webhooks", async (
    HttpRequest request,
    WebhookSignatureVerifier verifier) =>
{
    using var reader = new StreamReader(request.Body);
    var payload = await reader.ReadToEndAsync();
    var signature = request.Headers["X-Webhook-Signature"].ToString();

    return verifier.IsValid(payload, signature)
        ? Results.Accepted()
        : Results.Unauthorized();
});

app.Run();

public partial class Program;
