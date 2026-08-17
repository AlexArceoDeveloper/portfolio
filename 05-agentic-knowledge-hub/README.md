# Agentic Knowledge Hub

A provider-neutral .NET sample for retrieval-augmented generation, cited answers and allowlisted tool execution.

## Demonstrated concepts

- Chunk retrieval using deterministic vector-like scoring
- Answers grounded in retrieved sources with citations
- Tool allowlisting for agent workflows
- Separation between retrieval, orchestration and model-provider boundaries
- Prompt-injection resistance: retrieved text is treated as data, never as authority
- Dependency-free executable tests

This is a personal demonstration project. It does not claim professional Azure AI Foundry, Semantic Kernel or production RAG experience.

## Run

```bash
dotnet run --project src/KnowledgeHub.Api/KnowledgeHub.Api.csproj
```

## Test

```bash
dotnet run --project tests/KnowledgeHub.Tests/KnowledgeHub.Tests.csproj
```

## Planned adapters

- Azure AI Search
- Azure AI Foundry models
- Semantic Kernel orchestration
- OpenTelemetry tracing
- PostgreSQL with pgvector
