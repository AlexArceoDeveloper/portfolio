# ASP.NET Core API

Personal demonstration project for a maintainable REST API using C#, ASP.NET Core, Entity Framework Core, SQL Server, authentication and automated tests.

## Scope

- Clean architecture boundaries.
- REST endpoints with validation and error handling.
- EF Core persistence with SQL Server.
- xUnit integration and unit tests.
- OpenAPI documentation.
- Local container workflow as a learning track.

## Local verification

```powershell
dotnet build src/Portfolio.Api/Portfolio.Api.csproj
dotnet test tests/Portfolio.Api.Tests/Portfolio.Api.Tests.csproj
```
