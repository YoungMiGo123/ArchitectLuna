using System.Text;
using ArchitectLuna.Cli.Adapters;
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
        RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
        COPY --from=build /app/publish .
        ENV ASPNETCORE_URLS=http://+:8080
        EXPOSE 8080
        HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
          CMD curl -f http://localhost:8080/health || exit 1
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
                healthcheck:
                  test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
                  interval: 30s
                  timeout: 5s
                  retries: 3
                  start_period: 20s
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
                healthcheck:
                  test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -C -Q \"SELECT 1\" || exit 1"]
                  interval: 10s
                  timeout: 5s
                  retries: 10
                  start_period: 20s
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
                healthcheck:
                  test: ["CMD-SHELL", "pg_isready -U postgres"]
                  interval: 10s
                  timeout: 5s
                  retries: 10
                  start_period: 10s
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
            healthcheck:
              test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
              interval: 30s
              timeout: 5s
              retries: 3
              start_period: 20s
            depends_on:
              db:
                condition: service_healthy
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

    public static string ReadMe(string solutionName, string adapterName, string persistenceName, SolutionLayout layout, DatabaseApplyMode applyMode = DatabaseApplyMode.Manual)
    {
        var layoutName = layout == SolutionLayout.CleanArchitecture ? "clean-architecture" : "vertical-slice";
        var provider = PersistenceRegistry.ParseProvider(persistenceName);
        var applyModeName = DatabaseApplyModeParser.ToKebabCase(applyMode);
        var applicationProjectRoot = layout == SolutionLayout.CleanArchitecture ? $"{solutionName}.Application" : $"{solutionName}.Api";

        var sb = new StringBuilder();
        sb.AppendLine($"# {solutionName}");
        sb.AppendLine();
        sb.AppendLine("Generated by [ArchitectLuna](https://github.com/YoungMiGo123/ArchitectLuna)");
        sb.AppendLine($"(architecture: `{layoutName}`, adapter: `{adapterName}`, persistence: `{persistenceName}`, database apply mode: `{applyModeName}`).");
        sb.AppendLine();
        sb.AppendLine("The Intent Model at `.architect/model.yaml` is the source of truth — evolve the API with");
        sb.AppendLine("`architect-luna add feature/entity/command/query` followed by `architect-luna generate`.");
        sb.AppendLine("Hand-written logic inside `// <architect:region ...>` blocks survives regeneration.");
        sb.AppendLine();
        sb.AppendLine("## Run");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine($"dotnet run --project src/{solutionName}.Api");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Run with Docker Compose");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("docker compose up --build");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Adding a feature");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("architect-luna add feature Payments");
        sb.AppendLine("architect-luna add entity Payments PaymentRequest --field AmountCents:long --field Currency:string");
        sb.AppendLine("architect-luna generate");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("`add entity` synthesizes the full Create/Update/Delete/GetById/GetAll CRUD slice for you.");
        sb.AppendLine();
        sb.AppendLine("## Adding a field to an existing entity");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("architect-luna add field Payments PaymentRequest Reference:string");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Updates the entity, its persistence configuration, and every dependent command/query/validator/");
        sb.AppendLine("mapping/handler in the same run — there's no separate sync step to remember.");
        sb.AppendLine();
        sb.AppendLine("## Database apply mode");
        sb.AppendLine();
        sb.AppendLine($"Currently `{applyModeName}`. `manual` (default) applies nothing automatically — you run migrations");
        sb.AppendLine("yourself; `on-generate` applies changes when `architect-luna generate` runs; `on-startup` applies");
        sb.AppendLine("changes when the API process starts (only appropriate for internal/controlled environments —");
        sb.AppendLine("see `docs/local-development.md`). Change it with:");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("architect-luna config set database.applyMode on-startup");
        sb.AppendLine("```");

        if (provider is PersistenceProvider.EfCorePostgres or PersistenceProvider.EfCoreSqlServer)
        {
            sb.AppendLine();
            sb.AppendLine("## EF Core migrations");
            sb.AppendLine();
            sb.AppendLine("```bash");
            sb.AppendLine("dotnet ef migrations add Initial \\");
            sb.AppendLine($"  --project src/{solutionName}.Infrastructure \\");
            sb.AppendLine($"  --startup-project src/{solutionName}.Api");
            sb.AppendLine();
            sb.AppendLine("dotnet ef database update \\");
            sb.AppendLine($"  --project src/{solutionName}.Infrastructure \\");
            sb.AppendLine($"  --startup-project src/{solutionName}.Api");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine($"A design-time factory (`src/{solutionName}.Infrastructure/Persistence/{solutionName}DbContextFactory.cs`)");
            sb.AppendLine("lets these commands create the `DbContext` without running the app.");
        }
        else if (provider == PersistenceProvider.Marten)
        {
            sb.AppendLine();
            sb.AppendLine("## Marten schema handling");
            sb.AppendLine();
            sb.AppendLine("Marten has no separate migrations step. In `manual`/`on-generate` mode it only creates schema");
            sb.AppendLine("objects that don't exist yet; in `on-startup` mode it's also allowed to update existing ones,");
            sb.AppendLine("and the app applies all configured changes to the database once at process start.");
        }

        sb.AppendLine();
        sb.AppendLine("## Where business logic belongs");
        sb.AppendLine();
        sb.AppendLine($"Inside `src/{applicationProjectRoot}/Features/{{Feature}}/{{Operation}}/{{Operation}}Handler.cs`,");
        sb.AppendLine("in the `// <architect:region name=\"handler-body\">` block — everything else in a slice is");
        sb.AppendLine("regenerated from the model.");
        sb.AppendLine();
        sb.AppendLine("## Protected regions");
        sb.AppendLine();
        sb.AppendLine("Any `// <architect:region name=\"...\">...// </architect:region>` block survives regeneration:");
        sb.AppendLine("`generate` splices its previous contents back into the freshly rendered file, so hand-written");
        sb.AppendLine("logic never gets clobbered even as the surrounding scaffolding stays in sync with the model.");
        sb.AppendLine();
        sb.AppendLine("See `docs/architecture.md` for how the solution is laid out and");
        sb.AppendLine("`docs/local-development.md` for local setup (database, Docker, tests).");
        return sb.ToString();
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
            sb.AppendLine($"- `src/{solutionName}.Application` — use cases: commands/queries, handlers, validators, mappings, the Result pattern, abstractions (`IUserContext`, `IDateTimeProvider`). Never references Api or Infrastructure. Each operation's Request/Response DTOs live in a `Contracts/` subfolder of its own slice (e.g. `Features/{{Feature}}/{{Operation}}/Contracts/`) — there is no separate Contracts project.");
            sb.AppendLine($"- `src/{solutionName}.Infrastructure` — persistence and other technical implementations. References Application and Domain.");
            sb.AppendLine($"- `src/{solutionName}.Api` — HTTP concerns only: endpoints, middleware, composition root. May reference the Application feature slices' Contracts types; never defines its own DTOs.");
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
