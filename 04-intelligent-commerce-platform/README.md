# Intelligent Commerce Platform

A production-minded .NET reference project for payment orchestration, signed webhooks and explainable risk assessment.

## Why this project exists

Modern .NET vacancies increasingly combine backend APIs, cloud delivery, secure integrations, distributed-system reliability and applied AI. This project demonstrates those concerns without claiming that a real payment provider or AI model has processed production data.

## Features

- ASP.NET Core minimal API on .NET 10
- Provider-neutral payment gateway boundary
- Idempotent payment creation backed by PostgreSQL or an in-memory development store
- EF Core persistence with a unique idempotency constraint and race-safe reads
- HMAC-SHA256 webhook verification
- Explainable, deterministic risk assessment behind an AI-ready interface
- Health endpoint and RFC-compatible problem responses
- OpenTelemetry traces and metrics with an OTLP export path
- Docker image and local compose file
- Azure App Service Bicep sample
- Kubernetes deployment, probes, resource limits and secret references
- Dependency-free executable domain tests

## Run locally

```bash
dotnet run --project src/Commerce.Api/Commerce.Api.csproj
```

Without a connection string, the API selects the in-memory store. To run the API and PostgreSQL together:

```bash
docker compose up --build
```

`POSTGRES_PASSWORD` can override the local-only compose default. The API reads `ConnectionStrings__Commerce`; no credential is committed for a deployed environment.

The compose environment also starts an OpenTelemetry Collector and Jaeger. After creating a payment, inspect traces at `http://localhost:16686` and select the `commerce-api` service.

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

## Kubernetes demonstration

Build the local image, provide secrets directly to your cluster and apply the manifests:

```bash
docker build -t portfolio-commerce:local .
kubectl apply -f deploy/kubernetes/namespace.yaml
kubectl -n portfolio-demo create secret generic commerce-secrets \
  --from-literal=postgres-password='choose-a-local-secret' \
  --from-literal=webhook-secret='choose-a-local-secret'
kubectl apply -k deploy/kubernetes
```

`secret.example.yaml` documents the required keys and is deliberately excluded from the kustomization. Validate the rendered resources without a cluster using `kubectl kustomize deploy/kubernetes`.

## Security notes

- No credentials, tokens, real card data or payment-provider secrets are committed.
- The webhook secret comes from configuration and the development value is intentionally non-production.
- The API accepts order-level payment commands only; it never accepts or stores card numbers.
- Repeated commands with the same idempotency key return the original result.

## Architecture

```text
Client -> Payments API -> Risk assessor -> Payment gateway boundary
                    \-> Payment store -> PostgreSQL / in-memory adapter
Provider -> Signed webhook endpoint -> Signature verifier -> Payment state
```

## Next increments

- Add a sandbox adapter for a real payment provider.
- Connect the existing Blazor dashboard to this API.
- Add an Azure AI Foundry or Semantic Kernel adapter without changing domain contracts.
