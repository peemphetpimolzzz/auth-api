# Build context is the repository root (shared Directory.*.props).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY backend/src/AuthApi.Api/AuthApi.Api.csproj backend/src/AuthApi.Api/
RUN dotnet restore backend/src/AuthApi.Api/AuthApi.Api.csproj
COPY backend/ backend/
RUN dotnet publish backend/src/AuthApi.Api/AuthApi.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "AuthApi.Api.dll"]
