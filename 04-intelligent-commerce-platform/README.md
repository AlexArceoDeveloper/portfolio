# Intelligent Commerce Platform

A production-minded .NET reference project for payment orchestration, signed webhooks and explainable risk assessment.

## Why this project exists

Modern .NET vacancies increasingly combine backend APIs, cloud delivery, secure integrations, distributed-system reliability and applied AI. This project demonstrates those concerns without claiming that a real payment provider or AI model has processed production data.

## Features

- ASP.NET Core minimal API on .NET 10
- Provider-neutral payment gateway boundary
- Idempotent payment creation
- HMAC-SHA256 webhook verification
- Explainable, deterministic risk assessment behind an AI-ready interface
- Health endpoint and RFC-compatible problem responses
- Docker image and local compose file
- Azure App Service Bicep sample
- Dependency-free executable domain tests

## Run locally

```bash
dotnet run --project src/Commerce.Api/Commerce.Api.csproj
```

Create a payment:

```bash
curl -X POST http://localhost:5000/api/payments \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: demo-order-1001" \
  -d '{"orderId":"order-1001","amount":129.99,"currency":"EUR","customerRiskSignals":["new-device"]}'
```

Run the tests:

```bash
dotnet run --project tests/Commerce.Domain.Tests/Commerce.Domain.Tests.csproj
```

## Security notes

- No credentials, tokens, real card data or payment-provider secrets are committed.
- The webhook secret comes from configuration and the development value is intentionally non-production.
- The API accepts order-level payment commands only; it never accepts or stores card numbers.
- Repeated commands with the same idempotency key return the original result.

## Architecture

```text
Client -> Payments API -> Risk assessor -> Payment gateway boundary
                    \-> Idempotency store
Provider -> Signed webhook endpoint -> Signature verifier -> Payment state
```

## Next increments

- Replace in-memory stores with PostgreSQL and EF Core.
- Add OpenTelemetry traces and structured logs.
- Add a sandbox adapter for a real payment provider.
- Connect the existing Blazor dashboard to this API.
- Add an Azure AI Foundry or Semantic Kernel adapter without changing domain contracts.
