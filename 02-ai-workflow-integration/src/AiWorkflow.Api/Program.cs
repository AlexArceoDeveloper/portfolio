var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton<WorkflowOrchestrator>();
builder.Services.AddSingleton<IToolRegistry, AllowlistedToolRegistry>();

var app = builder.Build();
app.UseExceptionHandler();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/workflows/run", async (
    WorkflowRequest request,
    WorkflowOrchestrator orchestrator,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Objective))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.Objective)] = ["Objective is required."]
        });
    }

    var result = await orchestrator.RunAsync(request, cancellationToken);
    return Results.Ok(result);
});

app.Run();

public sealed record WorkflowRequest(string Objective, IReadOnlyCollection<string> RequestedTools);
public sealed record WorkflowStep(string Tool, string Status, string Message);
public sealed record WorkflowResult(Guid RunId, string Objective, IReadOnlyCollection<WorkflowStep> Steps);

public interface IToolRegistry
{
    bool IsAllowed(string toolName);
}

public sealed class AllowlistedToolRegistry : IToolRegistry
{
    private static readonly HashSet<string> AllowedTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "profile.read",
        "jobs.score",
        "report.generate"
    };

    public bool IsAllowed(string toolName) => AllowedTools.Contains(toolName);
}

public sealed class WorkflowOrchestrator(IToolRegistry registry)
{
    public Task<WorkflowResult> RunAsync(WorkflowRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var steps = request.RequestedTools
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(tool => registry.IsAllowed(tool)
                ? new WorkflowStep(tool, "approved", "Tool is allowlisted for this demonstration run.")
                : new WorkflowStep(tool, "blocked", "Tool is outside the allowlist and was not executed."))
            .ToArray();

        return Task.FromResult(new WorkflowResult(Guid.NewGuid(), request.Objective.Trim(), steps));
    }
}

public partial class Program;
