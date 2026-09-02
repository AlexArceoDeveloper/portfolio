# AI Workflow Control Center

An ASP.NET Core MVC reference application for planning governed AI-assisted workflows before they reach production systems. It turns a business request into an explainable route, processing stages, control checks and an explicit approval decision.

## Engineering focus

- Conventional ASP.NET Core MVC with controllers, strongly typed Razor views and server-side validation.
- Deterministic workflow planning that is straightforward to unit-test and replace with a model-backed classifier.
- Approval boundaries for external writes and confidential information.
- Responsive interface following the A# black, orange and ivory visual system.
- Dependency-free executable tests for fast local and CI validation.

## Run

```bash
dotnet run --project src/AiControlCenter/AiControlCenter.csproj
```

Open the local URL shown by ASP.NET Core, enter a workflow request and inspect the generated plan.

## Verify

```bash
dotnet build src/AiControlCenter/AiControlCenter.csproj --configuration Release
dotnet run --project tests/AiControlCenter.Tests/AiControlCenter.Tests.csproj --configuration Release
```

## Scope

This is a personal architecture demonstration. It uses synthetic requests and stores no credentials, employer information or production data.
