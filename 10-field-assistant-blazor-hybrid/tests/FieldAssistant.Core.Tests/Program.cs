using FieldAssistant.Core;

var localUri = EndpointPolicy.BuildAgentRunUri("http://localhost:8080/dashboard");
Assert(localUri == new Uri("http://localhost:8080/api/agents/run"),
    "The client must target the backend agent endpoint.");

var secureUri = EndpointPolicy.BuildAgentRunUri("https://api.example.com/v1");
Assert(secureUri.Scheme == Uri.UriSchemeHttps, "Remote endpoints must retain HTTPS.");

var rejectedInsecureRemote = false;
try
{
    EndpointPolicy.BuildAgentRunUri("http://api.example.com");
}
catch (ArgumentException)
{
    rejectedInsecureRemote = true;
}
Assert(rejectedInsecureRemote, "Insecure remote endpoints must be rejected.");

var outbox = new PromptOutbox();
var first = outbox.Enqueue(
    "Summarise the approved incident runbook",
    ["knowledge.search", "knowledge.search", "answer.compose"]);
var second = outbox.Enqueue(
    "Classify the service request",
    ["knowledge.search"]);

Assert(outbox.Count == 2, "Queued prompts must be counted.");
Assert(first.RequestedTools.Count == 2, "Duplicate tools must be removed.");
Assert(outbox.TryTake(out var dequeued) && dequeued?.Id == first.Id,
    "The outbox must preserve FIFO order.");
Assert(outbox.Snapshot().Single().Id == second.Id, "The remaining item must be inspectable.");

Console.WriteLine("Field Assistant core tests passed.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
