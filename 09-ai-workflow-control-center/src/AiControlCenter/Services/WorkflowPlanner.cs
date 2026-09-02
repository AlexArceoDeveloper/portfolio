using AiControlCenter.Models;

namespace AiControlCenter.Services;

public interface IWorkflowPlanner
{
    WorkflowPlan Build(AutomationRequest request);
}

public sealed class WorkflowPlanner : IWorkflowPlanner
{
    public WorkflowPlan Build(AutomationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Task))
        {
            throw new ArgumentException("A workflow request is required.", nameof(request));
        }

        var input = $"{request.BusinessArea} {request.Task}".ToLowerInvariant();
        var (route, confidence) = input switch
        {
            _ when ContainsAny(input, "citizen", "resident", "housing", "public")
                => ("Citizen support", 0.94m),
            _ when ContainsAny(input, "invoice", "payment", "finance", "purchase")
                => ("Finance automation", 0.92m),
            _ when ContainsAny(input, "incident", "ticket", "support", "outage")
                => ("Support triage", 0.91m),
            _ => ("Knowledge workflow", 0.84m)
        };

        var requiresApproval = request.WritesExternalSystems ||
                               request.Sensitivity == DataSensitivity.Confidential;

        var stages = new List<string>
        {
            "Classify intent and validate the request",
            "Retrieve approved knowledge with source references",
            "Compose a constrained response",
            "Evaluate policy and confidence thresholds"
        };
        if (request.WritesExternalSystems)
        {
            stages.Add("Hold the external action for an explicit decision");
        }
        stages.Add("Record the outcome with a correlation identifier");

        var controls = new List<string>
        {
            "Ground outputs in approved evidence",
            "Reject requests that fail input validation",
            "Attach route, confidence and trace metadata"
        };
        if (request.Sensitivity != DataSensitivity.Public)
        {
            controls.Add("Minimise and redact non-public data before model processing");
        }
        if (requiresApproval)
        {
            controls.Add("Require human approval before side effects");
        }

        var reason = requiresApproval
            ? "Approval is required because the request contains a controlled data or action boundary."
            : "The plan is read-only and can run within the automatic policy boundary.";

        return new WorkflowPlan(route, confidence, stages, controls, requiresApproval, reason);
    }

    private static bool ContainsAny(string input, params string[] terms) =>
        terms.Any(input.Contains);
}
