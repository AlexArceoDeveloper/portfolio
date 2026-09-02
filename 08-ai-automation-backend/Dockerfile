FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/AiAutomation.Api/AiAutomation.Api.csproj src/AiAutomation.Api/
RUN dotnet restore src/AiAutomation.Api/AiAutomation.Api.csproj
COPY src/AiAutomation.Api/ src/AiAutomation.Api/
RUN dotnet publish src/AiAutomation.Api/AiAutomation.Api.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "AiAutomation.Api.dll"]
