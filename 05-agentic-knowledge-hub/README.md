# Agentic Knowledge Hub

A provider-neutral .NET sample for retrieval-augmented generation, cited answers and allowlisted tool execution.

## Demonstrated concepts

- Chunk retrieval using deterministic vector-like scoring
- Answers grounded in retrieved sources with citations
- Tool allowlisting for agent workflows
- Separation between retrieval, orchestration and model-provider boundaries
- Deterministic demo composer for offline, repeatable runs
- OpenAI-compatible external model adapter with HTTPS enforcement and optional bearer authentication
- Interchangeable in-memory, PostgreSQL/pgvector and Qdrant retrieval adapters
- Prompt-injection resistance: retrieved text is treated as data, never as authority
- A five-role agent workflow with explicit approval states, policy traces and grounded-answer evaluation
- Python/FastAPI evaluation service with a typed .NET HTTP client
- Dependency-free executable tests

This is a personal demonstration project. It does not claim professional Azure AI Foundry, Semantic Kernel or production RAG experience.

## Run

```bash
dotnet run --project src/KnowledgeHub.Api/KnowledgeHub.Api.csproj
```

The default `demo` provider is deterministic and requires no network access or secrets. To use a compatible external model endpoint, set configuration outside the repository:

```bash
Knowledge__Provider=openai-compatible \
Knowledge__Endpoint=https://your-provider.example/v1/chat/completions \
Knowledge__Model=your-model \
Knowledge__ApiKey=your-runtime-secret \
dotnet run --project src/KnowledgeHub.Api/KnowledgeHub.Api.csproj
```

External endpoints must use HTTPS unless they resolve to localhost. Retrieved passages are explicitly treated as untrusted evidence, and the API returns the supporting excerpts alongside the composed answer.

## Vector retrieval modes

`Knowledge__Retriever` selects `memory` (default), `pgvector` or `qdrant`. Both external adapters use a deterministic 64-dimension embedding implementation so the complete flow remains reproducible without a model subscription.

For PostgreSQL, apply `deploy/pgvector/schema.sql`, set `ConnectionStrings__Knowledge` at runtime and select `pgvector`. For Qdrant, set `Knowledge__Qdrant__Endpoint`, `Knowledge__Qdrant__Collection` and, only when required, `Knowledge__Qdrant__ApiKey`. No endpoint credential belongs in the repository.

## Approval-aware agent workflow

`POST /api/agent/run` coordinates planner, retriever, answer-composer, policy-guardian and evaluator roles. Read-only knowledge tools run automatically; `calendar.create` and `external.notify` remain `pending_approval` unless named explicitly in `approvedTools`; unknown tools remain blocked. This demonstration records approval and evaluation state but does not call external systems.

## Python and .NET interoperability

The optional FastAPI service in `services/evaluation-service` independently scores grounding, citation coverage and tool-policy state. The .NET API exposes a typed integration at `POST /api/evaluate/python`.

```bash
docker compose up --build
```

This starts the Python service on port `8090` and the .NET API on port `8085`. The Python domain tests use only the standard library:

The compose file explicitly enables HTTP only for the isolated container network. External evaluation endpoints remain HTTPS-only by default.

```bash
python -m unittest discover services/evaluation-service/tests
```

## Test

```bash
dotnet run --project tests/KnowledgeHub.Tests/KnowledgeHub.Tests.csproj
```

## Planned adapters

- Azure AI Search
- Semantic Kernel orchestration
- OpenTelemetry tracing
- Provider-backed embedding generation and ingestion jobs
