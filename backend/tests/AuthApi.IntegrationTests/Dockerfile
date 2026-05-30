# Build context is the repository root.
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY backend/ backend/
RUN dotnet restore backend/tests/AuthApi.IntegrationTests/AuthApi.IntegrationTests.csproj
ENTRYPOINT ["dotnet", "test", \
  "backend/tests/AuthApi.IntegrationTests/AuthApi.IntegrationTests.csproj", \
  "-c", "Release", "--logger", "console;verbosity=normal"]
