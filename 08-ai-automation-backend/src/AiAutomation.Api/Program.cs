using System.Security.Cryptography;
using System.Text;
using AiAutomation.Api;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();

var persistenceProvider = builder.Configuration["Persistence:Provider"] ?? "memory";
if (persistenceProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required for PostgreSQL.");
    builder.Services.AddDbContext<AutomationDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddScoped<IKnowledgeSearch, EfKnowledgeSearch>();
}
else
{
    builder.Services.AddDbContext<AutomationDbContext>(options =>
        options.UseInMemoryDatabase("ai-automation-demo"));
    builder.Services.AddSingleton<IKnowledgeSearch>(_ => new InMemoryKnowledgeSearch(
    [
        new KnowledgeEvidence(
            Guid.NewGuid(),
            "Automation policy",
            "AI workflows must use approved evidence, explicit tool policies and observable service boundaries.",
            "https://example.invalid/automation-policy",
            1)
    ]));
}

builder.Services
    .AddIdentityCore<IdentityUser>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireNonAlphanumeric = true;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AutomationDbContext>();

var signingKeyText = builder.Configuration["Auth:SigningKey"];
var signingKey = string.IsNullOrWhiteSpace(signingKeyText)
    ? RandomNumberGenerator.GetBytes(64)
    : Encoding.UTF8.GetBytes(signingKeyText);
var jwtOptions = new JwtOptions(
    builder.Configuration["Auth:Issuer"] ?? "ai-automation-api",
    builder.Configuration["Auth:Audience"] ?? "ai-automation-client",
    signingKey);
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<JwtTokenService>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(jwtOptions.SigningKey),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("agent.execute", policy => policy.RequireAuthenticatedUser());

var modelProvider = builder.Configuration["AI:Provider"] ?? "deterministic";
if (modelProvider.Equals("openai-compatible", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton(new OpenAiOptions(
        builder.Configuration["AI:Endpoint"]
            ?? throw new InvalidOperationException("AI:Endpoint is required."),
        builder.Configuration["AI:Model"]
            ?? throw new InvalidOperationException("AI:Model is required."),
        builder.Configuration["AI:ApiKey"]));
    builder.Services.AddHttpClient<IAiModelClient, OpenAiCompatibleModelClient>();
}
else
{
    builder.Services.AddSingleton<IAiModelClient, DeterministicModelClient>();
}

var storageProvider = builder.Configuration["Storage:Provider"] ?? "memory";
if (storageProvider.Equals("s3", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton(new S3StorageOptions(
        builder.Configuration["Storage:Bucket"]
            ?? throw new InvalidOperationException("Storage:Bucket is required.")));
    builder.Services.AddSingleton<IAmazonS3>(_ =>
    {
        var serviceUrl = builder.Configuration["Storage:ServiceUrl"];
        return string.IsNullOrWhiteSpace(serviceUrl)
            ? new AmazonS3Client()
            : new AmazonS3Client(new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = true
            });
    });
    builder.Services.AddSingleton<IArtifactStore, S3ArtifactStore>();
}
else
{
    builder.Services.AddSingleton<IArtifactStore, MemoryArtifactStore>();
}

var routingProvider = builder.Configuration["Routing:Provider"] ?? "rules";
if (routingProvider.Equals("python", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton(new NeuralRouterOptions(
        builder.Configuration["Routing:Endpoint"]
            ?? throw new InvalidOperationException("Routing:Endpoint is required.")));
    builder.Services.AddHttpClient<IIntentRouter, PythonIntentRouter>();
}
else
{
    builder.Services.AddSingleton<IIntentRouter, RuleBasedIntentRouter>();
}

builder.Services.AddScoped<AgentWorkflowOrchestrator>();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("ai-automation-api"))
    .WithTracing(tracing => tracing
        .AddSource(AgentTelemetry.SourceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(AgentTelemetry.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<AutomationDbContext>();
    await database.Database.EnsureCreatedAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapPost("/api/auth/register", async (
    RegisterRequest request,
    UserManager<IdentityUser> users) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["credentials"] = ["Email and password are required."]
        });
    }

    var user = new IdentityUser { UserName = request.Email, Email = request.Email };
    var result = await users.CreateAsync(user, request.Password);
    return result.Succeeded
        ? Results.Created($"/api/users/{user.Id}", new { user.Id, user.Email })
        : Results.ValidationProblem(result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));
});
app.MapPost("/api/auth/token", async (
    TokenRequest request,
    UserManager<IdentityUser> users,
    JwtTokenService tokens) =>
{
    var user = await users.FindByEmailAsync(request.Email);
    if (user is null || !await users.CheckPasswordAsync(user, request.Password))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new { accessToken = tokens.Create(user, TimeSpan.FromMinutes(30)), expiresIn = 1800 });
});
app.MapPost("/api/knowledge", async (
    AddKnowledgeRequest request,
    AutomationDbContext database,
    CancellationToken cancellationToken) =>
{
    var item = new KnowledgeItem
    {
        Title = request.Title,
        Content = request.Content,
        SourceUrl = request.SourceUrl
    };
    database.KnowledgeItems.Add(item);
    await database.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/knowledge/{item.Id}", item);
}).RequireAuthorization("agent.execute");
app.MapPost("/api/agents/run", async (
    AgentRunRequest request,
    AgentWorkflowOrchestrator orchestrator,
    CancellationToken cancellationToken) =>
{
    var response = await orchestrator.RunAsync(request, cancellationToken);
    return Results.Ok(response);
}).RequireAuthorization("agent.execute");

app.Run();

public sealed record RegisterRequest(string Email, string Password);
public sealed record TokenRequest(string Email, string Password);
public sealed record AddKnowledgeRequest(string Title, string Content, string SourceUrl);
public partial class Program;
