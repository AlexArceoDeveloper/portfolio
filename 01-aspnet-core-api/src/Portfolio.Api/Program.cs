var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();

var deliveries = new List<DeliveryItem>
{
    new(1, "Portfolio API", "In progress", 0.65m),
    new(2, "AI workflow integration", "Planned", 0.10m),
};

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/deliveries", () =>
    TypedResults.Ok<IReadOnlyCollection<DeliveryItem>>(deliveries));

app.MapGet("/api/deliveries/{id:int}", (int id) =>
{
    var item = deliveries.FirstOrDefault(x => x.Id == id);
    return item is null ? Results.NotFound() : Results.Ok(item);
});

app.MapPost("/api/deliveries", (CreateDeliveryRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.Name)] = ["Name is required."]
        });
    }

    var nextId = deliveries.Count == 0 ? 1 : deliveries.Max(x => x.Id) + 1;
    var item = new DeliveryItem(nextId, request.Name.Trim(), request.Status.Trim(), request.Progress);
    deliveries.Add(item);
    return Results.Created($"/api/deliveries/{item.Id}", item);
});

app.Run();

public sealed record DeliveryItem(int Id, string Name, string Status, decimal Progress);
public sealed record CreateDeliveryRequest(string Name, string Status, decimal Progress);

public partial class Program;
