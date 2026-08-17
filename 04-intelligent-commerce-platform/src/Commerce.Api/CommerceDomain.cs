using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Commerce.Api;

public sealed record CreatePaymentCommand(
    string OrderId,
    decimal Amount,
    string Currency,
    IReadOnlyCollection<string>? CustomerRiskSignals);

public sealed record RiskAssessment(int Score, string Decision, IReadOnlyCollection<string> Reasons);

public sealed record PaymentResult(
    Guid PaymentId,
    string OrderId,
    decimal Amount,
    string Currency,
    string Status,
    RiskAssessment Risk,
    DateTimeOffset CreatedAt);

public interface IRiskAssessor
{
    RiskAssessment Assess(CreatePaymentCommand command);
}

public sealed class ExplainableRiskAssessor : IRiskAssessor
{
    public RiskAssessment Assess(CreatePaymentCommand command)
    {
        var reasons = new List<string>();
        var score = 0;

        if (command.Amount >= 1_000m)
        {
            score += 45;
            reasons.Add("High transaction amount.");
        }

        foreach (var signal in command.CustomerRiskSignals ?? [])
        {
            if (signal.Equals("new-device", StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
                reasons.Add("Customer is using a new device.");
            }
            else if (signal.Equals("velocity", StringComparison.OrdinalIgnoreCase))
            {
                score += 40;
                reasons.Add("Transaction velocity threshold was exceeded.");
            }
        }

        score = Math.Min(score, 100);
        var decision = score switch
        {
            >= 70 => "decline",
            >= 40 => "review",
            _ => "approve"
        };

        return new RiskAssessment(score, decision, reasons);
    }
}

public interface IPaymentGateway
{
    Task<string> AuthorizeAsync(CreatePaymentCommand command, CancellationToken cancellationToken);
}

public sealed class SandboxPaymentGateway : IPaymentGateway
{
    public Task<string> AuthorizeAsync(CreatePaymentCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var status = command.Amount <= 0 ? "rejected" : "authorized";
        return Task.FromResult(status);
    }
}

public sealed class PaymentService(IRiskAssessor riskAssessor, IPaymentGateway gateway)
{
    private readonly ConcurrentDictionary<string, PaymentResult> _paymentsByKey = new();

    public async Task<(PaymentResult Result, bool Created)> CreateAsync(
        string idempotencyKey,
        CreatePaymentCommand command,
        CancellationToken cancellationToken)
    {
        if (_paymentsByKey.TryGetValue(idempotencyKey, out var existing))
        {
            return (existing, false);
        }

        var risk = riskAssessor.Assess(command);
        var status = risk.Decision == "decline"
            ? "declined_by_risk_policy"
            : await gateway.AuthorizeAsync(command, cancellationToken);

        var result = new PaymentResult(
            Guid.NewGuid(),
            command.OrderId.Trim(),
            command.Amount,
            command.Currency.Trim().ToUpperInvariant(),
            status,
            risk,
            DateTimeOffset.UtcNow);

        var stored = _paymentsByKey.GetOrAdd(idempotencyKey, result);
        return (stored, ReferenceEquals(stored, result));
    }
}

public sealed class WebhookSignatureVerifier(string secret)
{
    public bool IsValid(string payload, string suppliedSignature)
    {
        if (string.IsNullOrWhiteSpace(suppliedSignature))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        var suppliedBytes = Encoding.ASCII.GetBytes(suppliedSignature.Trim().ToUpperInvariant());

        return expectedBytes.Length == suppliedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}
