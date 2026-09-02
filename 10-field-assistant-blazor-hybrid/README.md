# Field Assistant · .NET MAUI Blazor Hybrid

A Windows desktop client for the portfolio's [AI Automation Backend](../08-ai-automation-backend). The application combines a native .NET MAUI host with a reusable Blazor interface, typed HTTP integration and an offline-safe prompt outbox.

## Engineering focus

- .NET MAUI Blazor Hybrid on .NET 10.
- Shared C# contracts and integration logic in a platform-independent core library.
- Typed calls to `POST /api/agents/run` with an optional bearer token held in memory only.
- Local HTTP allowed only for loopback development; remote endpoints must use HTTPS.
- Offline queue stores the task and requested tools, never the access token.
- Explicit display of evidence, tool decisions and correlation identifiers.

## Run the backend

Follow the setup in [AI Automation Backend](../08-ai-automation-backend), register a local demonstration user and request an access token.

## Build the Windows client

```powershell
dotnet workload install maui-windows
dotnet build src/FieldAssistant.Hybrid/FieldAssistant.Hybrid.csproj --configuration Release --framework net10.0-windows10.0.19041.0
```

## Verify the core

```bash
dotnet build src/FieldAssistant.Core/FieldAssistant.Core.csproj --configuration Release
dotnet run --project tests/FieldAssistant.Core.Tests/FieldAssistant.Core.Tests.csproj --configuration Release
```

## Scope

This personal demonstration uses synthetic prompts and does not persist access tokens, credentials or production data.
