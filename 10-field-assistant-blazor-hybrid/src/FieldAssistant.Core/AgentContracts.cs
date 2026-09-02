namespace FieldAssistant.Core;

public sealed record AgentRunRequest(
    string Prompt,
    IReadOnlyCollection<string> RequestedTools,
    IReadOnlyCollection<string> ApprovedTools);

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
    string CorrelationId)
{
    public bool HasPendingApproval =>
        Tools.Any(tool => tool.Status.Equals("pending_approval", StringComparison.OrdinalIgnoreCase));
}

public sealed record QueuedPrompt(
    Guid Id,
    string Prompt,
    IReadOnlyCollection<string> RequestedTools,
    DateTimeOffset QueuedAt);
