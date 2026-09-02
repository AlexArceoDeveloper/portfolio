using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FieldAssistant.Core;

public sealed class AgentApiClient(HttpClient httpClient)
{
    public async Task<AgentRunResponse> RunAsync(
        string baseAddress,
        AgentRunRequest request,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new ArgumentException("A prompt is required.", nameof(request));
        }

        var endpoint = EndpointPolicy.BuildAgentRunUri(baseAddress);
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(request)
        };

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Trim());
        }

        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AgentRunResponse>(cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("The API returned an empty response.");
    }
}
