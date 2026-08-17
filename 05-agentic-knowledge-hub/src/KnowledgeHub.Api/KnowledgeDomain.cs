namespace KnowledgeHub.Api;

public sealed record KnowledgeDocument(string Id, string Title, string Content, string SourceUrl);
public sealed record SearchHit(string DocumentId, string Title, string Excerpt, string SourceUrl, double Score);
public sealed record AskRequest(string Question, IReadOnlyCollection<string>? RequestedTools);
public sealed record CitedAnswer(string Answer, IReadOnlyCollection<SearchHit> Sources, IReadOnlyCollection<ToolDecision> Tools);
public sealed record ToolDecision(string Name, string Status);

public interface IKnowledgeRetriever
{
    IReadOnlyCollection<SearchHit> Search(string query, int limit = 3);
}

public sealed class InMemoryKnowledgeRetriever(IEnumerable<KnowledgeDocument> documents) : IKnowledgeRetriever
{
    private readonly IReadOnlyCollection<KnowledgeDocument> _documents = documents.ToArray();

    public IReadOnlyCollection<SearchHit> Search(string query, int limit = 3)
    {
        var terms = Tokenize(query);

        return _documents
            .Select(document =>
            {
                var documentTerms = Tokenize($"{document.Title} {document.Content}");
                var overlap = terms.Count(term => documentTerms.Contains(term));
                var score = terms.Count == 0 ? 0 : (double)overlap / terms.Count;
                var excerpt = document.Content.Length <= 220
                    ? document.Content
                    : $"{document.Content[..220]}...";
                return new SearchHit(document.Id, document.Title, excerpt, document.SourceUrl, score);
            })
            .Where(hit => hit.Score > 0)
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.Title, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    private static HashSet<string> Tokenize(string value) => value
        .Split([' ', ',', '.', ':', ';', '-', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
        .Select(term => term.Trim().ToLowerInvariant())
        .Where(term => term.Length > 2)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

public sealed class AgentOrchestrator(IKnowledgeRetriever retriever)
{
    private static readonly HashSet<string> AllowedTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "knowledge.search",
        "answer.compose",
        "citation.validate"
    };

    public CitedAnswer Ask(AskRequest request)
    {
        var sources = retriever.Search(request.Question);
        var tools = (request.RequestedTools ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new ToolDecision(name, AllowedTools.Contains(name) ? "allowed" : "blocked"))
            .ToArray();

        var answer = sources.Count == 0
            ? "The available knowledge does not contain enough evidence to answer this question."
            : $"Found {sources.Count} relevant source(s). Review the cited excerpts before acting on the answer.";

        return new CitedAnswer(answer, sources, tools);
    }
}
