using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace KnowledgeHub.Api;

public interface IEmbeddingProvider
{
    int Dimensions { get; }
    ValueTask<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}

public sealed class DeterministicEmbeddingProvider(int dimensions = 64) : IEmbeddingProvider
{
    public int Dimensions { get; } = dimensions > 0
        ? dimensions
        : throw new ArgumentOutOfRangeException(nameof(dimensions));

    public ValueTask<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var vector = new float[Dimensions];
        foreach (var token in text.Split(
                     [' ', ',', '.', ':', ';', '-', '/', '(', ')'],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token.ToLowerInvariant()));
            var index = (int)(BitConverter.ToUInt32(hash, 0) % (uint)Dimensions);
            vector[index] += hash[4] % 2 == 0 ? 1f : -1f;
        }

        var length = MathF.Sqrt(vector.Sum(value => value * value));
        if (length > 0)
        {
            for (var index = 0; index < vector.Length; index++)
            {
                vector[index] /= length;
            }
        }

        return ValueTask.FromResult(vector);
    }
}

public static class VectorEncoding
{
    public static string ToPgVectorLiteral(IReadOnlyCollection<float> vector) =>
        $"[{string.Join(',', vector.Select(value => value.ToString("R", CultureInfo.InvariantCulture)))}]";
}

public sealed record PgVectorOptions(string ConnectionString, string TableName = "knowledge_documents")
{
    public string ValidatedTableName()
    {
        if (string.IsNullOrWhiteSpace(TableName) ||
            !char.IsLetter(TableName[0]) && TableName[0] != '_' ||
            TableName.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
        {
            throw new InvalidOperationException(
                "The pgvector table name may contain only letters, digits and underscores.");
        }

        return $"\"{TableName}\"";
    }
}

public sealed class PgVectorKnowledgeRetriever(
    PgVectorOptions options,
    IEmbeddingProvider embeddingProvider) : IKnowledgeRetriever
{
    public async Task<IReadOnlyCollection<SearchHit>> SearchAsync(
        string query,
        int limit = 3,
        CancellationToken cancellationToken = default)
    {
        var vector = await embeddingProvider.EmbedAsync(query, cancellationToken);
        await using var connection = new NpgsqlConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand($$"""
            SELECT id, title, content, source_url,
                   1 - (embedding <=> CAST(@embedding AS vector)) AS score
            FROM {{options.ValidatedTableName()}}
            ORDER BY embedding <=> CAST(@embedding AS vector)
            LIMIT @limit
            """, connection);
        command.Parameters.AddWithValue("embedding", VectorEncoding.ToPgVectorLiteral(vector));
        command.Parameters.AddWithValue("limit", Math.Clamp(limit, 1, 20));

        var hits = new List<SearchHit>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var content = reader.GetString(2);
            hits.Add(new SearchHit(
                reader.GetString(0),
                reader.GetString(1),
                content.Length <= 220 ? content : $"{content[..220]}...",
                reader.GetString(3),
                reader.GetDouble(4)));
        }

        return hits;
    }
}

public sealed record QdrantOptions(
    string Endpoint,
    string Collection,
    string? ApiKey)
{
    public Uri QueryEndpoint()
    {
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException("Knowledge:Qdrant:Endpoint must be an absolute URI.");
        }

        if (endpoint.Scheme != Uri.UriSchemeHttps && !endpoint.IsLoopback)
        {
            throw new InvalidOperationException(
                "Qdrant endpoints must use HTTPS unless they target localhost.");
        }

        return new Uri(
            $"{endpoint.ToString().TrimEnd('/')}/collections/{Uri.EscapeDataString(Collection)}/points/query");
    }
}

public sealed class QdrantKnowledgeRetriever(
    HttpClient httpClient,
    QdrantOptions options,
    IEmbeddingProvider embeddingProvider) : IKnowledgeRetriever
{
    public async Task<IReadOnlyCollection<SearchHit>> SearchAsync(
        string query,
        int limit = 3,
        CancellationToken cancellationToken = default)
    {
        var vector = await embeddingProvider.EmbedAsync(query, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, options.QueryEndpoint())
        {
            Content = JsonContent.Create(new
            {
                query = vector,
                limit = Math.Clamp(limit, 1, 20),
                with_payload = true,
                with_vector = false
            })
        };
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            request.Headers.Add("api-key", options.ApiKey);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var result = document.RootElement.GetProperty("result");
        var points = result.ValueKind == JsonValueKind.Object && result.TryGetProperty("points", out var nested)
            ? nested
            : result;

        var hits = new List<SearchHit>();
        foreach (var point in points.EnumerateArray())
        {
            var payload = point.GetProperty("payload");
            var content = RequiredString(payload, "content");
            hits.Add(new SearchHit(
                point.GetProperty("id").ToString(),
                RequiredString(payload, "title"),
                content.Length <= 220 ? content : $"{content[..220]}...",
                OptionalString(payload, "sourceUrl", "source_url") ?? "about:blank",
                point.GetProperty("score").GetDouble()));
        }

        return hits;
    }

    private static string RequiredString(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidOperationException($"Qdrant payload field '{name}' is required.");

    private static string? OptionalString(JsonElement payload, params string[] names)
    {
        foreach (var name in names)
        {
            if (payload.TryGetProperty(name, out var value))
            {
                return value.GetString();
            }
        }

        return null;
    }
}
