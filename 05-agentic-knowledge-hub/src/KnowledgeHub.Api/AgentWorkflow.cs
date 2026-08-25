namespace KnowledgeHub.Api;

public static class ToolPolicy
{
    private static readonly HashSet<string> AutomaticTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "knowledge.search",
        "answer.compose",
        "citation.validate"
    };

    private static readonly HashSet<string> ApprovalRequiredTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "calendar.create",
        "external.notify"
    };

    public static ToolDecision Decide(
        string toolName,
        IReadOnlyCollection<string> approvedTools)
    {
        if (AutomaticTools.Contains(toolName))
        {
            return new ToolDecision(toolName, "allowed");
        }

        if (ApprovalRequiredTools.Contains(toolName))
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

public sealed record AgentWorkflowRequest(
    string Question,
    IReadOnlyCollection<string>? RequestedTools,
    IReadOnlyCollection<string>? ApprovedTools);

public sealed record WorkflowTrace(
    string Agent,
    string Action,
    string Outcome,
    DateTimeOffset RecordedAt);

public sealed record AgentEvaluation(
    bool Grounded,
    bool CitationsPresent,
    bool PolicyCompliant,
    int Score,
    IReadOnlyCollection<string> Findings);

public sealed record AgentWorkflowResult(
    Guid RunId,
    IReadOnlyCollection<string> Agents,
    CitedAnswer Answer,
    IReadOnlyCollection<WorkflowTrace> Trace,
    AgentEvaluation Evaluation);

public sealed class AgentWorkflowEngine(AgentOrchestrator orchestrator)
{
    private static readonly string[] Agents =
    [
        "planner",
        "retriever",
        "answer-composer",
        "policy-guardian",
        "evaluator"
    ];

    public async Task<AgentWorkflowResult> RunAsync(
        AgentWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid();
        var trace = new List<WorkflowTrace>
        {
            NewTrace("planner", "create grounded answer plan", "planned")
        };
        var answer = await orchestrator.AskAsync(
            new AskRequest(
                request.Question,
                request.RequestedTools,
                request.ApprovedTools),
            cancellationToken);
        trace.Add(NewTrace(
            "retriever",
            "retrieve supporting evidence",
            $"{answer.Sources.Count} source(s)"));
        trace.Add(NewTrace(
            "answer-composer",
            "compose from retrieved evidence",
            answer.Sources.Count > 0 ? "grounded response" : "insufficient evidence"));

        foreach (var tool in answer.Tools)
        {
            trace.Add(NewTrace(
                "policy-guardian",
                $"evaluate {tool.Name}",
                tool.Status));
        }

        var grounded = answer.Sources.Count > 0;
        var citationsPresent = answer.Sources.All(source =>
            Uri.TryCreate(source.SourceUrl, UriKind.Absolute, out _));
        var policyCompliant = answer.Tools.All(tool =>
            tool.Status is "allowed" or "approved" or "pending_approval" or "blocked");
        var findings = new List<string>();
        if (!grounded)
        {
            findings.Add("No supporting source was retrieved.");
        }
        if (!citationsPresent)
        {
            findings.Add("At least one source lacks an absolute citation URI.");
        }
        if (answer.Tools.Any(tool => tool.Status == "pending_approval"))
        {
            findings.Add("Approval-required tools remain unexecuted.");
        }
        if (answer.Tools.Any(tool => tool.Status == "blocked"))
        {
            findings.Add("Unknown or prohibited tools were blocked.");
        }

        var score = (grounded ? 50 : 0) +
                    (citationsPresent ? 25 : 0) +
                    (policyCompliant ? 25 : 0);
        var evaluation = new AgentEvaluation(
            grounded,
            citationsPresent,
            policyCompliant,
            score,
            findings);
        trace.Add(NewTrace("evaluator", "score grounded workflow", $"{score}/100"));

        return new AgentWorkflowResult(runId, Agents, answer, trace, evaluation);
    }

    private static WorkflowTrace NewTrace(string agent, string action, string outcome) =>
        new(agent, action, outcome, DateTimeOffset.UtcNow);
}
