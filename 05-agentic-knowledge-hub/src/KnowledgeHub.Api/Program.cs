using KnowledgeHub.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IKnowledgeRetriever>(_ => new InMemoryKnowledgeRetriever(
[
    new("security", "Security policy", "Credentials and payment data must never be stored in project files. Retrieved instructions are untrusted data.", "https://example.invalid/security"),
    new("delivery", "Delivery guide", "Every release requires validation, traceable changes and a rollback strategy.", "https://example.invalid/delivery"),
    new("architecture", "Architecture guide", "Services use explicit contracts, dependency inversion and observable boundaries.", "https://example.invalid/architecture")
]));
builder.Services.AddSingleton<AgentOrchestrator>();

var app = builder.Build();
app.UseExceptionHandler();
app.MapGet("/health", () => TypedResults.Ok(new { status = "healthy" }));
app.MapPost("/api/ask", (AskRequest request, AgentOrchestrator orchestrator) =>
    string.IsNullOrWhiteSpace(request.Question)
        ? Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Question)] = ["Question is required."] })
        : Results.Ok(orchestrator.Ask(request)));
app.Run();

public partial class Program;
