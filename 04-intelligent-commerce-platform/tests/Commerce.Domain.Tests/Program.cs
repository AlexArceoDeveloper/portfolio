using Commerce.Api;
using System.Diagnostics;

var failures = new List<string>();
var activities = new List<Activity>();
using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == CommerceTelemetry.ActivitySourceName,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStopped = activity => activities.Add(activity)
};
ActivitySource.AddActivityListener(listener);

void Assert(bool condition, string message)
{
    if (!condition)
    {
        failures.Add(message);
    }
}

var assessor = new ExplainableRiskAssessor();
var lowRisk = assessor.Assess(new CreatePaymentCommand("order-1", 99m, "EUR", []));
Assert(lowRisk.Decision == "approve", "Low-risk payments should be approved.");

var highRisk = assessor.Assess(new CreatePaymentCommand(
    "order-2",
    1_500m,
    "EUR",
    ["new-device", "velocity"]));
Assert(highRisk.Decision == "decline", "High-risk payments should be declined.");

var store = new InMemoryPaymentStore();
var service = new PaymentService(assessor, new SandboxPaymentGateway(), store);
var command = new CreatePaymentCommand("order-3", 129.99m, "EUR", ["new-device"]);
var first = await service.CreateAsync("stable-key", command, CancellationToken.None);
var second = await service.CreateAsync("stable-key", command, CancellationToken.None);
Assert(first.Created, "The first idempotent request should create a payment.");
Assert(!second.Created, "A repeated idempotency key should reuse the payment.");
Assert(first.Result.PaymentId == second.Result.PaymentId, "Repeated requests should return the same payment.");
var persisted = await store.FindAsync("stable-key", CancellationToken.None);
Assert(persisted?.PaymentId == first.Result.PaymentId, "The payment store should retain the result.");
Assert(
    activities.Any(activity => activity.OperationName == "payment.create"),
    "Payment processing should emit a trace activity.");

var verifier = new WebhookSignatureVerifier("test-secret");
Assert(!verifier.IsValid("payload", "invalid"), "Invalid webhook signatures should be rejected.");

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

Console.WriteLine("All commerce domain tests passed.");
return 0;
