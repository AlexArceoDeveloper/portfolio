using KnowledgeHub.Api;

var documents = new[]
{
    new KnowledgeDocument("1", "Security", "Never store credentials or payment data in project files.", "https://example.invalid/security"),
    new KnowledgeDocument("2", "Delivery", "Validate every release and retain a rollback strategy.", "https://example.invalid/delivery")
};

var retriever = new InMemoryKnowledgeRetriever(documents);
var securityHits = retriever.Search("How should credentials be stored?");
if (securityHits.FirstOrDefault()?.DocumentId != "1")
{
    Console.Error.WriteLine("Retrieval did not rank the security source first.");
    return 1;
}

var answer = new AgentOrchestrator(retriever).Ask(new AskRequest(
    "How should credentials be stored?",
    ["knowledge.search", "system.delete"]));

if (answer.Sources.Count == 0 ||
    answer.Tools.Single(tool => tool.Name == "knowledge.search").Status != "allowed" ||
    answer.Tools.Single(tool => tool.Name == "system.delete").Status != "blocked")
{
    Console.Error.WriteLine("Agent safety decisions were incorrect.");
    return 1;
}

Console.WriteLine("All knowledge hub tests passed.");
return 0;
