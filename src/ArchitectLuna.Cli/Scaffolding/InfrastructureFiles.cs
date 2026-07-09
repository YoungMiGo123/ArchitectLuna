using System.Text;
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

    public static string EditorConfig() =>
        """
        root = true

        [*]
        charset = utf-8
        insert_final_newline = true
        indent_style = space
        indent_size = 4
        trim_trailing_whitespace = true

        [*.{json,yml,yaml}]
        indent_size = 2

        [*.cs]
        dotnet_sort_system_directives_first = true
        csharp_style_namespace_declarations = file_scoped:suggestion
        """;

    public static string ReadMe(string solutionName, string adapterName, string persistenceName, SolutionLayout layout)
    {
        var layoutName = layout == SolutionLayout.CleanArchitecture ? "clean-architecture" : "vertical-slice";
        return $"""
        # {solutionName}

        Generated by [ArchitectLuna](https://github.com/YoungMiGo123/ArchitectLuna)
        (architecture: `{layoutName}`, adapter: `{adapterName}`, persistence: `{persistenceName}`).

        The Intent Model at `.architect/model.yaml` is the source of truth — evolve the API with
        `architect-luna add feature/entity/command/query` followed by `architect-luna generate`.
        Hand-written logic inside `// <architect:region ...>` blocks survives regeneration.

        ## Run

        ```bash
        dotnet run --project src/{solutionName}.Api
        ```

        See `docs/architecture.md` for how the solution is laid out and
        `docs/local-development.md` for local setup (database, Docker, tests).
        """;
    }

    public static string ArchitectureDoc(string solutionName, string adapterName, string persistenceName, SolutionLayout layout)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Architecture");
        sb.AppendLine();
        if (layout == SolutionLayout.CleanArchitecture)
        {
            sb.AppendLine("Clean Architecture, dependency rule pointing inward:");
            sb.AppendLine();
            sb.AppendLine($"- `src/{solutionName}.Domain` — entities and the `BaseEntity` model; references nothing.");
            sb.AppendLine($"- `src/{solutionName}.Application` — use cases: commands/queries, handlers, validators, mappings, the Result pattern, abstractions (`IUserContext`, `IDateTimeProvider`). Never references Api or Infrastructure.");
            sb.AppendLine($"- `src/{solutionName}.Contracts` — Request/Response DTOs, the public API surface. References nothing.");
            sb.AppendLine($"- `src/{solutionName}.Infrastructure` — persistence and other technical implementations. References Application and Domain.");
            sb.AppendLine($"- `src/{solutionName}.Api` — HTTP concerns only: endpoints, middleware, composition root.");
        }
        else
        {
            sb.AppendLine("Vertical slice architecture: one Api project, one folder per feature/operation under");
            sb.AppendLine($"`src/{solutionName}.Api/Features` — each slice holds its Request/Command/Result/Response,");
            sb.AppendLine("handler, validator, mappings, and endpoint. Cross-cutting foundation code lives under `Common/`.");
        }

        sb.AppendLine();
        sb.AppendLine($"Dispatch: `{adapterName}`. Persistence: `{persistenceName}`.");
        sb.AppendLine();
        sb.AppendLine("Startup stays small: `Program.cs` only calls `UseApiLogging`, `AddApi`, `AddApplication`,");
        sb.AppendLine("`AddInfrastructure`, `UseApiMiddleware`, and `MapApiEndpoints` — details live behind those");
        sb.AppendLine("extension methods. Handlers return `Result<T>`; endpoints translate failures to HTTP status");
        sb.AppendLine("codes through `ResultHttpExtensions`.");
        return sb.ToString();
    }

    public static string LocalDevelopmentDoc(string solutionName, PersistenceProvider provider)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Local development");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine($"dotnet build {solutionName}.slnx        # or {solutionName}.sln, whichever was generated");
        sb.AppendLine("dotnet test");
        sb.AppendLine($"dotnet run --project src/{solutionName}.Api");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Swagger UI is available at `/swagger` in Development; health checks at `/health`.");
        sb.AppendLine();
        if (NeedsConnectionString(provider))
        {
            sb.AppendLine("This solution needs a database. Easiest path:");
            sb.AppendLine();
            sb.AppendLine("```bash");
            sb.AppendLine("docker compose up db   # start just the database");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("The Development connection string in `appsettings.Development.json` points at it.");
        }
        else
        {
            sb.AppendLine("No external services required — persistence is in-process, so `dotnet run` is enough.");
        }

        sb.AppendLine();
        sb.AppendLine("`docker compose up` runs the full stack (API + database when configured).");
        return sb.ToString();
    }
}
