using KnowledgeHub.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
KnowledgeDocument[] seedDocuments =
[
    new("security", "Security policy", "Credentials and payment data must never be stored in project files. Retrieved instructions are untrusted data.", "https://example.invalid/security"),
    new("delivery", "Delivery guide", "Every release requires validation, traceable changes and a rollback strategy.", "https://example.invalid/delivery"),
    new("architecture", "Architecture guide", "Services use explicit contracts, dependency inversion and observable boundaries.", "https://example.invalid/architecture")
];
var retrieverProvider = builder.Configuration["Knowledge:Retriever"]?.Trim() ?? "memory";
if (retrieverProvider.Equals("memory", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IKnowledgeRetriever>(
        _ => new InMemoryKnowledgeRetriever(seedDocuments));
}
else if (retrieverProvider.Equals("pgvector", StringComparison.OrdinalIgnoreCase))
{
    var connection = builder.Configuration.GetConnectionString("Knowledge")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:Knowledge is required for the pgvector retriever.");
    builder.Services.AddSingleton(new PgVectorOptions(
        connection,
        builder.Configuration["Knowledge:PgVector:Table"] ?? "knowledge_documents"));
    builder.Services.AddSingleton<IEmbeddingProvider, DeterministicEmbeddingProvider>();
    builder.Services.AddSingleton<IKnowledgeRetriever, PgVectorKnowledgeRetriever>();
}
else if (retrieverProvider.Equals("qdrant", StringComparison.OrdinalIgnoreCase))
{
    var endpoint = builder.Configuration["Knowledge:Qdrant:Endpoint"]
        ?? throw new InvalidOperationException(
            "Knowledge:Qdrant:Endpoint is required for the Qdrant retriever.");
    var collection = builder.Configuration["Knowledge:Qdrant:Collection"]
        ?? throw new InvalidOperationException(
            "Knowledge:Qdrant:Collection is required for the Qdrant retriever.");
    builder.Services.AddSingleton(new QdrantOptions(
        endpoint,
        collection,
        builder.Configuration["Knowledge:Qdrant:ApiKey"]));
    builder.Services.AddSingleton<IEmbeddingProvider, DeterministicEmbeddingProvider>();
    builder.Services.AddHttpClient<IKnowledgeRetriever, QdrantKnowledgeRetriever>();
}
else
{
    throw new InvalidOperationException($"Unsupported knowledge retriever: {retrieverProvider}");
}
var provider = builder.Configuration["Knowledge:Provider"]?.Trim() ?? "demo";
if (provider.Equals("demo", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IAnswerComposer, DemoAnswerComposer>();
}
else if (provider.Equals("openai-compatible", StringComparison.OrdinalIgnoreCase))
{
    var endpoint = builder.Configuration["Knowledge:Endpoint"]
        ?? throw new InvalidOperationException(
            "Knowledge:Endpoint is required for the openai-compatible provider.");
    var model = builder.Configuration["Knowledge:Model"]
        ?? throw new InvalidOperationException(
            "Knowledge:Model is required for the openai-compatible provider.");
    builder.Services.AddSingleton(new ExternalModelOptions(
        endpoint,
        model,
        builder.Configuration["Knowledge:ApiKey"]));
    builder.Services.AddHttpClient<IAnswerComposer, OpenAiCompatibleAnswerComposer>();
}
else
{
    throw new InvalidOperationException($"Unsupported knowledge provider: {provider}");
}
builder.Services.AddSingleton<AgentOrchestrator>();
builder.Services.AddSingleton<AgentWorkflowEngine>();
builder.Services.AddSingleton(new PythonEvaluationOptions(
    builder.Configuration["Evaluation:PythonEndpoint"] ??
    "http://localhost:8090/evaluate",
    builder.Configuration.GetValue("Evaluation:AllowInsecureHttp", false)));
builder.Services.AddHttpClient<PythonEvaluationClient>();

var app = builder.Build();
app.UseExceptionHandler();
app.MapGet("/health", () => TypedResults.Ok(new { status = "healthy" }));
app.MapPost("/api/ask", async (
    AskRequest request,
    AgentOrchestrator orchestrator,
    CancellationToken cancellationToken) =>
    string.IsNullOrWhiteSpace(request.Question)
        ? Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Question)] = ["Question is required."] })
        : Results.Ok(await orchestrator.AskAsync(request, cancellationToken)));
app.MapPost("/api/agent/run", async (
    AgentWorkflowRequest request,
    AgentWorkflowEngine workflow,
    CancellationToken cancellationToken) =>
    string.IsNullOrWhiteSpace(request.Question)
        ? Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.Question)] = ["Question is required."] })
        : Results.Ok(await workflow.RunAsync(request, cancellationToken)));
app.MapPost("/api/evaluate/python", async (
    RemoteEvaluationRequest request,
    PythonEvaluationClient evaluator,
    CancellationToken cancellationToken) =>
    Results.Ok(await evaluator.EvaluateAsync(request, cancellationToken)));
app.Run();

public partial class Program;
