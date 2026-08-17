# Architecture

The sample separates orchestration from tool authorization.

1. The API receives a structured workflow request.
2. The orchestrator normalises requested tools.
3. The registry checks every tool against an explicit allowlist.
4. The response records approved and blocked steps.

No external AI provider, credential or production integration is required for the local demonstration. A provider adapter can be added later behind a dedicated interface.
