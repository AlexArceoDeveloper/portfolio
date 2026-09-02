using System.ComponentModel.DataAnnotations;

namespace AiControlCenter.Models;

public enum DataSensitivity
{
    Public,
    Internal,
    Confidential
}

public sealed class AutomationRequest
{
    [Required]
    [StringLength(240, MinimumLength = 10)]
    [Display(Name = "Workflow request")]
    public string Task { get; set; } = "Classify a citizen request and draft a grounded response";

    [Required]
    [StringLength(80)]
    [Display(Name = "Business area")]
    public string BusinessArea { get; set; } = "Citizen services";

    [Range(1, 500)]
    [Display(Name = "Estimated requests per day")]
    public int EstimatedDailyVolume { get; set; } = 40;

    [Display(Name = "Data sensitivity")]
    public DataSensitivity Sensitivity { get; set; } = DataSensitivity.Internal;

    [Display(Name = "The workflow writes to an external system")]
    public bool WritesExternalSystems { get; set; }
}

public sealed record WorkflowPlan(
    string Route,
    decimal Confidence,
    IReadOnlyList<string> Stages,
    IReadOnlyList<string> Controls,
    bool RequiresApproval,
    string DecisionReason);

public sealed class ControlCenterViewModel
{
    public AutomationRequest Input { get; set; } = new();
    public WorkflowPlan? Plan { get; set; }
}
