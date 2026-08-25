using System.Net.Http.Json;

namespace Commerce.Companion;

public sealed record PaymentDraft(string OrderId, decimal Amount, string Currency);
public sealed record RiskResult(int Score, string Decision, IReadOnlyCollection<string> Reasons);
public sealed record PaymentResponse(
    Guid PaymentId,
    string OrderId,
    decimal Amount,
    string Currency,
    string Status,
    RiskResult Risk,
    DateTimeOffset CreatedAt);

public sealed class CommerceClient(HttpClient httpClient)
{
    public async Task<PaymentResponse> CreatePaymentAsync(
        Uri apiBaseUri,
        PaymentDraft draft,
        CancellationToken cancellationToken)
    {
        var endpoint = new Uri(apiBaseUri, "/api/payments");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                draft.OrderId,
                draft.Amount,
                draft.Currency,
                CustomerRiskSignals = Array.Empty<string>()
            })
        };
        request.Headers.Add("Idempotency-Key", $"maui-{draft.OrderId}-{Guid.NewGuid():N}");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaymentResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The payment API returned an empty response.");
    }
}
