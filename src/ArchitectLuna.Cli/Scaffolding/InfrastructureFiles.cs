using ArchitectLuna.Core.Model;

namespace ArchitectLuna.Cli.Scaffolding;

/// <summary>
/// Everything a generated solution needs to run outside "just compiles": containerization,
/// per-environment configuration, and a working F5 experience. Shared by both
/// <see cref="SolutionLayout.VerticalSlice"/> and <see cref="SolutionLayout.CleanArchitecture"/> —
/// none of it depends on how many projects the source is split across, only on the solution name
/// and the chosen persistence provider.
/// </summary>
public static class InfrastructureFiles
{
    public static string Dockerfile(string solutionName) =>
        $$"""
        FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
        WORKDIR /src
        COPY . .
        RUN dotnet restore "{{solutionName}}.sln"
        RUN dotnet publish "src/{{solutionName}}.Api/{{solutionName}}.Api.csproj" -c Release -o /app/publish --no-restore

        FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
        WORKDIR /app
        COPY --from=build /app/publish .
        ENV ASPNETCORE_URLS=http://+:8080
        EXPOSE 8080
        ENTRYPOINT ["dotnet", "{{solutionName}}.Api.dll"]
        """;

    /// <summary>True only for providers that need an actual connection string / external database — not `none` or the zero-setup `in-memory` store.</summary>
    private static bool NeedsConnectionString(PersistenceProvider provider) =>
        provider is not PersistenceProvider.None and not PersistenceProvider.InMemory;

    public static string DockerCompose(string solutionName, PersistenceProvider provider)
    {
        if (!NeedsConnectionString(provider))
        {
            return $$"""
            services:
              api:
                build:
                  context: .
                  dockerfile: Dockerfile
                ports:
                  - "8080:8080"
                environment:
                  ASPNETCORE_ENVIRONMENT: Development
            """;
        }

        var dbServiceYaml = provider == PersistenceProvider.EfCoreSqlServer
            ? $$"""

              db:
                image: mcr.microsoft.com/mssql/server:2022-latest
                environment:
                  ACCEPT_EULA: "Y"
                  MSSQL_SA_PASSWORD: "YourStrong!Passw0rd"
                ports:
                  - "1433:1433"
                volumes:
                  - db-data:/var/opt/mssql
            """
            : $$"""

              db:
                image: postgres:16
                environment:
                  POSTGRES_DB: {{solutionName.ToLowerInvariant()}}
                  POSTGRES_USER: postgres
                  POSTGRES_PASSWORD: postgres
                ports:
                  - "5432:5432"
                volumes:
                  - db-data:/var/lib/postgresql/data
            """;

        return $$"""
        services:
          api:
            build:
              context: .
              dockerfile: Dockerfile
            ports:
              - "8080:8080"
            environment:
              ASPNETCORE_ENVIRONMENT: Development
              ConnectionStrings__Default: "{{DockerConnectionString(solutionName, provider)}}"
            depends_on:
              - db
        {{dbServiceYaml}}

        volumes:
          db-data:
        """;
    }

    public static string LaunchSettings() =>
        """
        {
          "$schema": "https://json.schemastore.org/launchsettings.json",
          "profiles": {
            "http": {
              "commandName": "Project",
              "dotnetRunMessages": true,
              "launchBrowser": false,
              "applicationUrl": "http://localhost:5080",
              "environmentVariables": {
                "ASPNETCORE_ENVIRONMENT": "Development"
              }
            },
            "https": {
              "commandName": "Project",
              "dotnetRunMessages": true,
              "launchBrowser": false,
              "applicationUrl": "https://localhost:5443;http://localhost:5080",
              "environmentVariables": {
                "ASPNETCORE_ENVIRONMENT": "Development"
              }
            }
          }
        }
        """;

    public static string AppSettings(PersistenceProvider provider)
    {
        var connectionStringsBlock = NeedsConnectionString(provider)
            ? "\n  \"ConnectionStrings\": {\n    \"Default\": \"\"\n  },"
            : string.Empty;

        return $$"""
        {{{connectionStringsBlock}}
          "Logging": {
            "LogLevel": {
              "Default": "Information",
              "Microsoft.AspNetCore": "Warning"
            }
          },
          "Serilog": {
            "MinimumLevel": {
              "Default": "Information",
              "Override": {
                "Microsoft.AspNetCore": "Warning"
              }
            }
          },
          "AllowedHosts": "*"
        }
        """;
    }

    public static string AppSettingsDevelopment(string solutionName, PersistenceProvider provider)
    {
        if (!NeedsConnectionString(provider))
        {
            return "{\n}";
        }

        return $$"""
        {
          "ConnectionStrings": {
            "Default": "{{LocalConnectionString(solutionName, provider)}}"
          }
        }
        """;
    }

    public const string GitIgnoreContent =
        """
        bin/
        obj/
        .vs/
        *.user
        appsettings.Local.json
        """;

    private static string LocalConnectionString(string solutionName, PersistenceProvider provider) => provider switch
    {
        PersistenceProvider.EfCorePostgres or PersistenceProvider.Marten =>
            $"Host=localhost;Database={solutionName.ToLowerInvariant()};Username=postgres;Password=postgres",
        PersistenceProvider.EfCoreSqlServer =>
            $"Server=localhost;Database={solutionName};Trusted_Connection=True;TrustServerCertificate=True",
        _ => string.Empty,
    };

    private static string DockerConnectionString(string solutionName, PersistenceProvider provider) => provider switch
    {
        PersistenceProvider.EfCorePostgres or PersistenceProvider.Marten =>
            $"Host=db;Database={solutionName.ToLowerInvariant()};Username=postgres;Password=postgres",
        PersistenceProvider.EfCoreSqlServer =>
            $"Server=db;Database={solutionName};User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True",
        _ => string.Empty,
    };
}
