FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Commerce.Api/Commerce.Api.csproj src/Commerce.Api/
RUN dotnet restore src/Commerce.Api/Commerce.Api.csproj
COPY . .
RUN dotnet publish src/Commerce.Api/Commerce.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Commerce.Api.dll"]
