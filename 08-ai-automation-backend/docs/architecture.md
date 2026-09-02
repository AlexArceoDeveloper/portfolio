# Architecture decisions

## Secure by default

Authentication uses ASP.NET Core Identity and short-lived JWTs. The signing key is supplied at runtime or generated ephemerally for local development. External model endpoints must use HTTPS unless they resolve to localhost, and secrets are read only from runtime configuration.

## Replaceable boundaries

The orchestrator depends on four interfaces: knowledge retrieval, model completion, artifact storage and intent routing. Deterministic in-memory implementations keep tests and local exploration repeatable. PostgreSQL, OpenAI-compatible, AWS S3 and Python/FastAPI adapters demonstrate production integration without coupling the domain workflow to a vendor.

## Agent and RAG safety

Retrieved passages are evidence, not instructions. The model receives a fixed system constraint, cited context and a bounded set of tool descriptions. Read-only tools can execute automatically; mutation-capable tools require explicit approval; unknown tools are blocked. Every decision is returned in a traceable response.

## Observability

The API creates spans around agent runs, retrieval, model calls, Python routing and artifact persistence. Counters record workflow completion and blocked tools. ASP.NET Core and outbound HTTP instrumentation flow through the same OTLP pipeline so a request can be followed across service boundaries.

## Reliability and scale

- Cancellation tokens cross every I/O boundary.
- Stateless API instances can scale horizontally.
- PostgreSQL owns durable workflow and knowledge metadata.
- S3 owns larger artifacts and model outputs.
- Correlation identifiers support retries and incident analysis.
- Provider interfaces make failure isolation and substitution explicit.
