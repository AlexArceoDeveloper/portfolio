# AI Workflow Integration

Personal demonstration project showing a safe AI workflow with explicit tool boundaries, validation and traceable outputs.

## Implemented foundation

- ASP.NET Core service layer.
- AI provider abstraction.
- Structured tool calls with allowlisted operations.
- Input validation and failure handling.
- Prompt and response audit model without storing secrets.
- Testable orchestration patterns inspired by professional automation work.

## Run locally

```powershell
dotnet run --project src/AiWorkflow.Api/AiWorkflow.Api.csproj
```

The first version uses a deterministic allowlisted workflow instead of external credentials, making the security boundary easy to review and test.
