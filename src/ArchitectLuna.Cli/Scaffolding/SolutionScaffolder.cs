using System.Diagnostics;
using ArchitectLuna.Cli.Adapters;
using ArchitectLuna.Core.Generation;
using ArchitectLuna.Core.Manifest;
using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Yaml;

namespace ArchitectLuna.Cli.Scaffolding;

/// <summary>
/// Scaffolds a brand-new API solution for `new api`. Shells out to the real `dotnet` CLI for
/// .sln creation, project registration, and package references so version resolution always
/// comes from the live NuGet feed instead of a hardcoded, potentially stale version pin.
/// </summary>
public static class SolutionScaffolder
{
    public static string Scaffold(string parentDirectory, string solutionName, string adapterName, string persistenceName = "none")
    {
        var root = Path.Combine(parentDirectory, solutionName);
        if (Directory.Exists(root))
        {
            throw new InvalidOperationException($"Directory '{root}' already exists.");
        }

        var persistenceProvider = PersistenceRegistry.ParseProvider(persistenceName);
        var adapter = AdapterRegistry.Resolve(adapterName);
        var persistence = PersistenceRegistry.Resolve(persistenceName);

        Directory.CreateDirectory(root);
        RunDotnet(root, "new", "sln", "-n", solutionName);

        var apiProjectRelative = Path.Combine("src", $"{solutionName}.Api");
        var apiProjectDir = Path.Combine(root, apiProjectRelative);
        Directory.CreateDirectory(apiProjectDir);

        var csprojPath = Path.Combine(apiProjectDir, $"{solutionName}.Api.csproj");
        File.WriteAllText(csprojPath, BuildCsproj());
        File.WriteAllText(Path.Combine(apiProjectDir, "Program.cs"), BuildProgramCs(solutionName, adapterName, persistence));

        var commonDir = Path.Combine(apiProjectDir, "Common");
        Directory.CreateDirectory(commonDir);
        File.WriteAllText(Path.Combine(commonDir, "IEndpointDefinition.cs"), BuildEndpointDefinitionInterface(solutionName));

        if (persistenceProvider != PersistenceProvider.None)
        {
            File.WriteAllText(Path.Combine(apiProjectDir, "appsettings.json"), BuildAppSettings(solutionName, persistenceProvider));
        }

        RunDotnet(root, "sln", "add", Path.Combine(apiProjectRelative, $"{solutionName}.Api.csproj"));

        foreach (var package in adapter.RequiredPackages.Concat(persistence.RequiredPackages))
        {
            RunDotnet(root, "add", csprojPath, "package", package);
        }

        Directory.CreateDirectory(Path.Combine(root, ".architect"));
        var model = new ArchitectModel
        {
            SolutionName = solutionName,
            Namespace = solutionName,
            Adapter = adapterName,
            Persistence = persistenceProvider,
            Features = new List<FeatureModel>(),
        };
        ModelSerializer.Save(Path.Combine(root, ".architect", "model.yaml"), model);
        ManifestStore.Save(Path.Combine(root, ".architect", "manifest.json"), new GenerationManifest());

        File.WriteAllText(Path.Combine(root, ".gitignore"), GitIgnoreContent);

        return root;
    }

    private static string BuildCsproj() =>
        """
        <Project Sdk="Microsoft.NET.Sdk.Web">

          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>

        </Project>
        """;

    private static string BuildProgramCs(string solutionName, string adapterName, IPersistenceGenerator persistence)
    {
        var (adapterUsings, bootstrapLines) = adapterName switch
        {
            "mediatr" => (
                new[] { "FluentValidation", "MediatR" },
                new[]
                {
                    "builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));",
                    "builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);",
                }),
            "wolverine" => (
                new[] { "FluentValidation", "Wolverine" },
                new[]
                {
                    "builder.Host.UseWolverine();",
                    "builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);",
                }),
            _ => throw new InvalidOperationException($"Unknown adapter '{adapterName}'."),
        };

        var usings = adapterUsings
            .Concat(persistence.ProgramCsUsings)
            .Concat(new[] { $"{solutionName}.Common" })
            .Distinct()
            .Select(u => $"using {u};");

        var bodyLines = bootstrapLines.Concat(persistence.BuildProgramCsRegistration(solutionName));

        var sb = new System.Text.StringBuilder();
        foreach (var u in usings)
        {
            sb.AppendLine(u);
        }

        sb.AppendLine();
        sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
        sb.AppendLine();
        foreach (var line in bodyLines)
        {
            sb.AppendLine(line);
        }

        sb.AppendLine();
        sb.AppendLine("var app = builder.Build();");
        sb.AppendLine();
        sb.AppendLine("// Generated Get/Update handlers throw KeyNotFoundException for a missing id; map that to a");
        sb.AppendLine("// proper 404 instead of letting it surface as an unhandled 500.");
        sb.AppendLine("app.Use(async (context, next) =>");
        sb.AppendLine("{");
        sb.AppendLine("    try");
        sb.AppendLine("    {");
        sb.AppendLine("        await next();");
        sb.AppendLine("    }");
        sb.AppendLine("    catch (KeyNotFoundException ex)");
        sb.AppendLine("    {");
        sb.AppendLine("        context.Response.StatusCode = StatusCodes.Status404NotFound;");
        sb.AppendLine("        await context.Response.WriteAsJsonAsync(new { error = ex.Message });");
        sb.AppendLine("    }");
        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine("foreach (var endpointType in typeof(Program).Assembly.GetTypes()");
        sb.AppendLine("    .Where(t => typeof(IEndpointDefinition).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))");
        sb.AppendLine("{");
        sb.AppendLine("    var endpoint = (IEndpointDefinition)Activator.CreateInstance(endpointType)!;");
        sb.AppendLine("    endpoint.Map(app);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.Append("app.Run();");
        return sb.ToString();
    }

    private static string BuildEndpointDefinitionInterface(string solutionName) =>
        $$"""
        namespace {{solutionName}}.Common;

        public interface IEndpointDefinition
        {
            void Map(IEndpointRouteBuilder app);
        }
        """;

    private static string BuildAppSettings(string solutionName, PersistenceProvider provider)
    {
        var connectionString = provider switch
        {
            PersistenceProvider.EfCorePostgres => $"Host=localhost;Database={solutionName.ToLowerInvariant()};Username=postgres;Password=postgres",
            PersistenceProvider.EfCoreSqlServer => $"Server=localhost;Database={solutionName};Trusted_Connection=True;TrustServerCertificate=True",
            PersistenceProvider.Marten => $"Host=localhost;Database={solutionName.ToLowerInvariant()};Username=postgres;Password=postgres",
            _ => string.Empty,
        };

        return $$"""
        {
          "ConnectionStrings": {
            "Default": "{{connectionString}}"
          },
          "Logging": {
            "LogLevel": {
              "Default": "Information",
              "Microsoft.AspNetCore": "Warning"
            }
          }
        }
        """;
    }

    private const string GitIgnoreContent =
        """
        bin/
        obj/
        """;

    private static void RunDotnet(string workingDirectory, params string[] arguments)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start 'dotnet' process.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'dotnet {string.Join(' ', arguments)}' failed with exit code {process.ExitCode}.\n{stdout}\n{stderr}");
        }
    }
}
