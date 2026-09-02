using AiControlCenter.Models;
using AiControlCenter.Services;

var planner = new WorkflowPlanner();

var citizenPlan = planner.Build(new AutomationRequest
{
    Task = "Classify a resident housing request and draft a grounded answer",
    BusinessArea = "Citizen services",
    EstimatedDailyVolume = 80,
    Sensitivity = DataSensitivity.Internal,
    WritesExternalSystems = false
});

Assert(citizenPlan.Route == "Citizen support", "Citizen requests must use the citizen-support route.");
Assert(!citizenPlan.RequiresApproval, "A read-only request must stay inside the automatic boundary.");
Assert(citizenPlan.Controls.Any(control => control.Contains("redact", StringComparison.OrdinalIgnoreCase)),
    "Non-public data must receive a redaction control.");

var financePlan = planner.Build(new AutomationRequest
{
    Task = "Validate an invoice and update the payment provider",
    BusinessArea = "Finance",
    Sensitivity = DataSensitivity.Confidential,
    WritesExternalSystems = true
});

Assert(financePlan.Route == "Finance automation", "Invoice requests must use the finance route.");
Assert(financePlan.RequiresApproval, "External writes must require approval.");
Assert(financePlan.Stages.Any(stage => stage.Contains("Hold", StringComparison.OrdinalIgnoreCase)),
    "Controlled actions must be held before execution.");

Console.WriteLine("AI Workflow Control Center tests passed.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
