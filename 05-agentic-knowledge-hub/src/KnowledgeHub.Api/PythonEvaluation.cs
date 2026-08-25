using System.Net.Http.Json;

namespace KnowledgeHub.Api;

public sealed record PythonEvaluationOptions(string Endpoint, bool AllowInsecureHttp = false)
{
    public Uri ValidatedEndpoint()
    {
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException("Evaluation:PythonEndpoint must be an absolute URI.");
        }

        if (endpoint.Scheme != Uri.UriSchemeHttps && !endpoint.IsLoopback && !AllowInsecureHttp)
        {
            throw new InvalidOperationException(
                "Python evaluation endpoints must use HTTPS unless they target localhost or insecure HTTP is explicitly enabled for an isolated development network.");
        }

        return endpoint;
    }
}

public sealed record RemoteEvaluationRequest(
    string Answer,
    int SourceCount,
    int CitationCount,
    IReadOnlyCollection<string> ToolStatuses);

public sealed record RemoteEvaluationResult(
    bool Grounded,
    double CitationCoverage,
    bool PolicyCompliant,
    int Score,
    IReadOnlyCollection<string> Findings);

public sealed class PythonEvaluationClient(
    HttpClient httpClient,
    PythonEvaluationOptions options)
{
    public async Task<RemoteEvaluationResult> EvaluateAsync(
        RemoteEvaluationRequest evaluation,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            options.ValidatedEndpoint(),
            evaluation,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RemoteEvaluationResult>(cancellationToken)
            ?? throw new InvalidOperationException(
                "The Python evaluation service returned an empty response.");
    }
}
