using AiAutomation.Api;
using Microsoft.AspNetCore.Identity;

var evidence = new[]
{
    new KnowledgeEvidence(
        Guid.NewGuid(),
        "Incident automation",
        "Incidents are classified, grounded in approved runbooks and routed through explicit tool policies.",
        "https://example.invalid/incidents",
        1)
};
var orchestrator = new AgentWorkflowOrchestrator(
    new InMemoryKnowledgeSearch(evidence),
    new DeterministicModelClient(),
    new MemoryArtifactStore(),
    new RuleBasedIntentRouter());

var response = await orchestrator.RunAsync(new AgentRunRequest(
    "How should an incident be routed?",
    ["knowledge.search", "external.notify", "system.delete"],
    []));

if (response.Intent != "support" ||
    response.Evidence.Count != 1 ||
    response.Tools.Single(tool => tool.Name == "knowledge.search").Status != "allowed" ||
    response.Tools.Single(tool => tool.Name == "external.notify").Status != "pending_approval" ||
    response.Tools.Single(tool => tool.Name == "system.delete").Status != "blocked" ||
    !response.ArtifactKey.EndsWith("/answer.txt", StringComparison.Ordinal))
{
    Console.Error.WriteLine("The grounded workflow or tool policy produced an unexpected result.");
    return 1;
}

var signingKey = Enumerable.Range(0, 64).Select(value => (byte)value).ToArray();
var tokens = new JwtTokenService(new JwtOptions("tests", "tests", signingKey));
var token = tokens.Create(
    new IdentityUser { Id = "user-1", Email = "developer@example.invalid" },
    TimeSpan.FromMinutes(5));
if (token.Count(character => character == '.') != 2)
{
    Console.Error.WriteLine("JWT token issuance failed.");
    return 1;
}

Console.WriteLine("All AI automation backend tests passed.");
return 0;
