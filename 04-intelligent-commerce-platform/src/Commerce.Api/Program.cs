using Commerce.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IRiskAssessor, ExplainableRiskAssessor>();
builder.Services.AddSingleton<IPaymentGateway, SandboxPaymentGateway>();
builder.Services.AddSingleton<PaymentService>();
builder.Services.AddSingleton(new WebhookSignatureVerifier(
    builder.Configuration["Payments:WebhookSecret"] ?? "development-only-secret"));

var app = builder.Build();
app.UseExceptionHandler();

app.MapGet("/health", () => TypedResults.Ok(new { status = "healthy" }));

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
