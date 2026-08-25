using Telemetry.Web;

var failures = new List<string>();

void Assert(bool condition, string message)
{
    if (!condition)
    {
        failures.Add(message);
    }
}

Assert(TelemetryRules.Evaluate(60, 3, 8) == "normal", "Normal readings were misclassified.");
Assert(TelemetryRules.Evaluate(72, 3, 8) == "warning", "Warning readings were misclassified.");
Assert(TelemetryRules.Evaluate(60, 13, 8) == "critical", "Critical readings were misclassified.");

var store = new InMemoryTelemetryStore();
var simulator = new TelemetrySimulator(new LocalTelemetryPublisher(store));
var generated = await simulator.PublishAsync("line-test");
var latest = await store.LatestAsync(10);
Assert(latest.Single().EventId == generated.EventId, "The simulated event was not stored.");
Assert(latest.Single().RecordedAt.Offset == TimeSpan.Zero, "Stored timestamps must be normalized to UTC.");

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

Console.WriteLine("All industrial telemetry tests passed.");
return 0;
