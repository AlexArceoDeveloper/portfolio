using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AiAutomation.Api;

public sealed record AgentRunRequest(
    string Prompt,
    IReadOnlyCollection<string>? RequestedTools,
    IReadOnlyCollection<string>? ApprovedTools);

public sealed record KnowledgeEvidence(
    Guid Id,
    string Title,
    string Excerpt,
    string SourceUrl,
    double Score);

public sealed record ToolDecision(string Name, string Status);

public sealed record AgentRunResponse(
    Guid RunId,
    string Answer,
    string Intent,
    IReadOnlyCollection<KnowledgeEvidence> Evidence,
    IReadOnlyCollection<ToolDecision> Tools,
    string ArtifactKey,
    string CorrelationId);

public interface IKnowledgeSearch
{
    Task<IReadOnlyCollection<KnowledgeEvidence>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);
}

public interface IAiModelClient
{
    Task<string> CompleteAsync(
        string prompt,
        IReadOnlyCollection<KnowledgeEvidence> evidence,
        IReadOnlyCollection<ToolDecision> tools,
        CancellationToken cancellationToken);
}

public interface IArtifactStore
{
    Task<string> SaveAsync(
        Guid runId,
        string content,
        CancellationToken cancellationToken);
}

public interface IIntentRouter
{
    Task<string> ClassifyAsync(string prompt, CancellationToken cancellationToken);
}

public static class AgentToolPolicy
{
    private static readonly HashSet<string> Automatic = new(StringComparer.OrdinalIgnoreCase)
    {
        "knowledge.search",
        "answer.compose",
        "artifact.read"
    };

    private static readonly HashSet<string> RequiresApproval = new(StringComparer.OrdinalIgnoreCase)
    {
        "artifact.write",
        "external.notify"
    };

    public static ToolDecision Decide(
        string toolName,
        IReadOnlyCollection<string> approvedTools)
    {
        if (Automatic.Contains(toolName))
        {
            return new ToolDecision(toolName, "allowed");
        }

        if (RequiresApproval.Contains(toolName))
        {
            return new ToolDecision(
                toolName,
                approvedTools.Contains(toolName, StringComparer.OrdinalIgnoreCase)
                    ? "approved"
                    : "pending_approval");
        }

        return new ToolDecision(toolName, "blocked");
    }
}

public static class AgentTelemetry
{
    public const string SourceName = "AiAutomation.AgentWorkflow";
    public const string MeterName = "AiAutomation.AgentWorkflow";
    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(MeterName);
    public static readonly Counter<long> CompletedRuns = Meter.CreateCounter<long>("agent.runs.completed");
    public static readonly Counter<long> BlockedTools = Meter.CreateCounter<long>("agent.tools.blocked");
}

public sealed class AgentWorkflowOrchestrator(
    IKnowledgeSearch knowledgeSearch,
    IAiModelClient modelClient,
    IArtifactStore artifactStore,
    IIntentRouter intentRouter)
{
    public async Task<AgentRunResponse> RunAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new ArgumentException("A prompt is required.", nameof(request));
        }

        using var activity = AgentTelemetry.ActivitySource.StartActivity("agent.run");
        var runId = Guid.NewGuid();
        activity?.SetTag("agent.run_id", runId);

        var requestedTools = request.RequestedTools ?? [];
        var approvedTools = request.ApprovedTools ?? [];
        var decisions = requestedTools
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(tool => AgentToolPolicy.Decide(tool, approvedTools))
            .ToArray();

        var blockedCount = decisions.Count(decision => decision.Status == "blocked");
        if (blockedCount > 0)
        {
            AgentTelemetry.BlockedTools.Add(blockedCount);
        }

        var evidenceTask = knowledgeSearch.SearchAsync(request.Prompt, 4, cancellationToken);
        var intentTask = intentRouter.ClassifyAsync(request.Prompt, cancellationToken);
        await Task.WhenAll(evidenceTask, intentTask);

        var evidence = await evidenceTask;
        var intent = await intentTask;
        var answer = evidence.Count == 0
            ? "The approved knowledge base does not contain enough evidence to answer this request."
            : await modelClient.CompleteAsync(
                request.Prompt,
                evidence,
                decisions,
                cancellationToken);

        var artifactKey = await artifactStore.SaveAsync(runId, answer, cancellationToken);
        var correlationId = Activity.Current?.TraceId.ToString() ?? runId.ToString("N");
        AgentTelemetry.CompletedRuns.Add(1, new KeyValuePair<string, object?>("intent", intent));

        return new AgentRunResponse(
            runId,
            answer,
            intent,
            evidence,
            decisions,
            artifactKey,
            correlationId);
    }
}

public sealed class DeterministicModelClient : IAiModelClient
{
    public Task<string> CompleteAsync(
        string prompt,
        IReadOnlyCollection<KnowledgeEvidence> evidence,
        IReadOnlyCollection<ToolDecision> tools,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var citations = string.Join(", ", evidence.Select((item, index) => $"[{index + 1}] {item.Title}"));
        return Task.FromResult($"Grounded response for '{prompt}' using {citations}.");
    }
}

public sealed class RuleBasedIntentRouter : IIntentRouter
{
    public Task<string> ClassifyAsync(string prompt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = prompt.ToLowerInvariant();
        var intent = normalized.Contains("invoice", StringComparison.Ordinal) ||
                     normalized.Contains("payment", StringComparison.Ordinal)
            ? "finance"
            : normalized.Contains("incident", StringComparison.Ordinal) ||
              normalized.Contains("error", StringComparison.Ordinal)
                ? "support"
                : "knowledge";
        return Task.FromResult(intent);
    }
}

public sealed class InMemoryKnowledgeSearch(IEnumerable<KnowledgeEvidence> evidence) : IKnowledgeSearch
{
    private readonly IReadOnlyCollection<KnowledgeEvidence> _evidence = evidence.ToArray();

    public Task<IReadOnlyCollection<KnowledgeEvidence>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var terms = Tokenize(query);
        IReadOnlyCollection<KnowledgeEvidence> results = _evidence
            .Select(item => item with
            {
                Score = terms.Count == 0
                    ? 0
                    : terms.Count(term => Tokenize($"{item.Title} {item.Excerpt}").Contains(term)) /
                      (double)terms.Count
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .Take(Math.Clamp(limit, 1, 20))
            .ToArray();
        return Task.FromResult(results);
    }

    private static HashSet<string> Tokenize(string value) => value
        .Split([' ', ',', '.', ':', ';', '-', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
        .Select(term => term.ToLowerInvariant())
        .Where(term => term.Length > 2)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

public sealed class MemoryArtifactStore : IArtifactStore
{
    private readonly Dictionary<string, string> _artifacts = new(StringComparer.Ordinal);

    public Task<string> SaveAsync(
        Guid runId,
        string content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = $"agent-runs/{runId:N}/answer.txt";
        _artifacts[key] = content;
        return Task.FromResult(key);
    }
}
