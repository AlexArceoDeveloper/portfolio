# Commerce Companion

A .NET MAUI Windows client for the [Intelligent Commerce Platform](../04-intelligent-commerce-platform). It demonstrates a native client, dependency injection, HTTP integration, validation and a responsive dark interface aligned with the portfolio identity.

## Scope

- Creates sandbox payment commands against the real commerce API contract.
- Generates an idempotency key for every user-initiated command.
- Displays the payment and explainable risk result.
- Restricts clear-text HTTP to localhost and never collects card data or credentials.
- Reuses the canonical A# visual asset.

## Build

```bash
dotnet build src/Commerce.Companion/Commerce.Companion.csproj \
  --configuration Release \
  --framework net10.0-windows10.0.19041.0
```

Start the commerce API with `docker compose up --build` in project 04, then run this project and keep the default `http://localhost:8080` endpoint.
