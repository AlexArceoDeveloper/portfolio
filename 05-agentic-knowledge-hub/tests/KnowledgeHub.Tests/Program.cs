using KnowledgeHub.Api;

var documents = new[]
{
    new KnowledgeDocument("1", "Security", "Never store credentials or payment data in project files.", "https://example.invalid/security"),
    new KnowledgeDocument("2", "Delivery", "Validate every release and retain a rollback strategy.", "https://example.invalid/delivery")
};

var retriever = new InMemoryKnowledgeRetriever(documents);
var securityHits = await retriever.SearchAsync("How should credentials be stored?");
if (securityHits.FirstOrDefault()?.DocumentId != "1")
{
    Console.Error.WriteLine("Retrieval did not rank the security source first.");
    return 1;
}

var answer = await new AgentOrchestrator(retriever, new DemoAnswerComposer()).AskAsync(new AskRequest(
    "How should credentials be stored?",
    ["knowledge.search", "system.delete"]));

if (answer.Sources.Count == 0 ||
    answer.Tools.Single(tool => tool.Name == "knowledge.search").Status != "allowed" ||
    answer.Tools.Single(tool => tool.Name == "system.delete").Status != "blocked")
{
    Console.Error.WriteLine("Agent safety decisions were incorrect.");
    return 1;
}

if (!answer.Answer.Contains("Security", StringComparison.Ordinal))
{
    Console.Error.WriteLine("The demo provider did not compose a grounded answer.");
    return 1;
}

var secureEndpoint = new ExternalModelOptions(
    "https://models.example.invalid/v1/chat/completions",
    "demo-model",
    null).ValidatedEndpoint();
if (secureEndpoint.Scheme != Uri.UriSchemeHttps)
{
    Console.Error.WriteLine("The external model adapter did not enforce a secure endpoint.");
    return 1;
}

var embeddingProvider = new DeterministicEmbeddingProvider(8);
var firstEmbedding = await embeddingProvider.EmbedAsync("secure delivery");
var secondEmbedding = await embeddingProvider.EmbedAsync("secure delivery");
if (!firstEmbedding.SequenceEqual(secondEmbedding) ||
    !VectorEncoding.ToPgVectorLiteral([0.5f, -0.25f]).Equals("[0.5,-0.25]", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Vector encoding must be deterministic and culture-invariant.");
    return 1;
}

using var qdrantClient = new HttpClient(new StubHandler("""
    {
      "result": {
        "points": [
          {
            "id": "security",
            "score": 0.93,
            "payload": {
              "title": "Security",
              "content": "Never store credentials in project files.",
              "sourceUrl": "https://example.invalid/security"
            }
          }
        ]
      }
    }
    """));
var qdrant = new QdrantKnowledgeRetriever(
    qdrantClient,
    new QdrantOptions("https://vectors.example.invalid", "knowledge", null),
    embeddingProvider);
var qdrantHits = await qdrant.SearchAsync("credentials");
if (qdrantHits.Single().DocumentId != "security")
{
    Console.Error.WriteLine("The Qdrant adapter did not map the provider response.");
    return 1;
}

var workflow = await new AgentWorkflowEngine(
    new AgentOrchestrator(retriever, new DemoAnswerComposer())).RunAsync(
        new AgentWorkflowRequest(
            "How should credentials be stored?",
            ["knowledge.search", "calendar.create", "system.delete"],
            []));
if (workflow.Answer.Tools.Single(tool => tool.Name == "calendar.create").Status != "pending_approval" ||
    workflow.Answer.Tools.Single(tool => tool.Name == "system.delete").Status != "blocked" ||
    workflow.Evaluation.Score != 100 ||
    !workflow.Agents.Contains("policy-guardian"))
{
    Console.Error.WriteLine("The multi-agent approval and evaluation workflow was incorrect.");
    return 1;
}

var approvedWorkflow = await new AgentWorkflowEngine(
    new AgentOrchestrator(retriever, new DemoAnswerComposer())).RunAsync(
        new AgentWorkflowRequest(
            "How should credentials be stored?",
            ["calendar.create"],
            ["calendar.create"]));
if (approvedWorkflow.Answer.Tools.Single().Status != "approved")
{
    Console.Error.WriteLine("Explicit approval was not preserved in the workflow trace.");
    return 1;
}

using var evaluationClient = new HttpClient(new StubHandler("""
    {
      "grounded": true,
      "citationCoverage": 1.0,
      "policyCompliant": true,
      "score": 100,
      "findings": []
    }
    """));
var remoteEvaluation = await new PythonEvaluationClient(
    evaluationClient,
    new PythonEvaluationOptions("http://localhost:8090/evaluate")).EvaluateAsync(
        new RemoteEvaluationRequest(
            approvedWorkflow.Answer.Answer,
            approvedWorkflow.Answer.Sources.Count,
            approvedWorkflow.Answer.Sources.Count,
            approvedWorkflow.Answer.Tools.Select(tool => tool.Status).ToArray()));
if (!remoteEvaluation.Grounded || remoteEvaluation.Score != 100)
{
    Console.Error.WriteLine("The .NET client did not map the Python evaluation response.");
    return 1;
}

Console.WriteLine("All knowledge hub tests passed.");
return 0;

file sealed class StubHandler(string json) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage
    {
        StatusCode = System.Net.HttpStatusCode.OK,
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    });
}
