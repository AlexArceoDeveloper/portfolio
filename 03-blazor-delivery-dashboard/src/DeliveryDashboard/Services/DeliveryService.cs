namespace DeliveryDashboard.Services;

public sealed record DeliveryStatus(string Name, string Status, int Progress, string Summary);

public sealed class DeliveryService
{
    private static readonly IReadOnlyCollection<DeliveryStatus> Items =
    [
        new("ASP.NET Core API", "In progress", 65, "REST endpoints, validation and health checks."),
        new("AI workflow integration", "Foundation complete", 45, "Allowlisted tools and auditable orchestration."),
        new("Blazor dashboard", "In progress", 35, "Accessible delivery reporting and responsive layout.")
    ];

    public IReadOnlyCollection<DeliveryStatus> GetItems() => Items;
}
