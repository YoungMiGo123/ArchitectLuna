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
///
/// Produces a solution that compiles and runs immediately, in either layout:
/// <see cref="SolutionLayout.VerticalSlice"/> (one Api project, features live inside it) or
/// <see cref="SolutionLayout.CleanArchitecture"/> (Api/Application/Domain/Infrastructure as four
/// real projects, dependency rule pointing inward) — both get Swagger, health checks, exception
/// middleware, DI, logging, Docker, and a test project, so there's nothing left to bolt on by hand
/// before the first `generate` run.
/// </summary>
public static class SolutionScaffolder
{
    public static string Scaffold(string parentDirectory, string solutionName, string adapterName, string persistenceName = "in-memory", SolutionLayout layout = SolutionLayout.VerticalSlice)
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
        File.WriteAllText(Path.Combine(root, "Directory.Build.props"), ProjectFiles.DirectoryBuildProps);

        var (apiCsprojRelative, context) = layout == SolutionLayout.CleanArchitecture
            ? ScaffoldCleanArchitecture(root, solutionName, adapterName, adapter, persistence)
            : ScaffoldVerticalSlice(root, solutionName, adapterName, adapter, persistence);

        // So a fresh scaffold compiles before the first `generate`: Program.cs already references
        // the DbContext type as soon as EF Core persistence is configured, so it must already exist
        // (with zero DbSets — `generate` re-renders it with real ones once entities are added).
        foreach (var file in persistence.GenerateSolutionPersistence(context, Array.Empty<EntityReference>()))
        {
            var fullPath = Path.Combine(root, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, file.Content);
        }

        File.WriteAllText(Path.Combine(root, "Dockerfile"), InfrastructureFiles.Dockerfile(solutionName));
        File.WriteAllText(Path.Combine(root, "docker-compose.yml"), InfrastructureFiles.DockerCompose(solutionName, persistenceProvider));
        File.WriteAllText(Path.Combine(root, ".gitignore"), InfrastructureFiles.GitIgnoreContent);

        Directory.CreateDirectory(Path.Combine(root, ".architect"));
        var model = new ArchitectModel
        {
            SolutionName = solutionName,
            Namespace = solutionName,
            Adapter = adapterName,
            Persistence = persistenceProvider,
            Layout = layout,
            Features = new List<FeatureModel>(),
        };
        ModelSerializer.Save(Path.Combine(root, ".architect", "model.yaml"), model);
        ManifestStore.Save(Path.Combine(root, ".architect", "manifest.json"), new GenerationManifest());

        if (layout == SolutionLayout.CleanArchitecture)
        {
            TestProjectScaffolder.CreateApplicationTests(root, solutionName, Path.Combine("src", $"{solutionName}.Application", $"{solutionName}.Application.csproj"));
        }

        TestProjectScaffolder.CreateApiTests(root, solutionName, apiCsprojRelative);

        return root;
    }

    private static (string ApiCsprojRelative, GenerationContext Context) ScaffoldVerticalSlice(string root, string solutionName, string adapterName, IFrameworkAdapter adapter, IPersistenceGenerator persistence)
    {
        var apiProjectRelative = Path.Combine("src", $"{solutionName}.Api");
        var apiProjectDir = Path.Combine(root, apiProjectRelative);
        Directory.CreateDirectory(apiProjectDir);

        var context = GenerationContext.ForVerticalSlice(solutionName, $"src/{solutionName}.Api");

        var csprojPath = Path.Combine(apiProjectDir, $"{solutionName}.Api.csproj");
        File.WriteAllText(csprojPath, ProjectFiles.WebProject());
        WriteApiProjectFiles(apiProjectDir, context, adapterName, persistence, PersistenceRegistry.ParseProvider(persistence.Name));

        var apiCsprojRelative = Path.Combine(apiProjectRelative, $"{solutionName}.Api.csproj");
        RunDotnet(root, "sln", "add", apiCsprojRelative);

        foreach (var package in adapter.RequiredPackages.Concat(persistence.RequiredPackages).Concat(SharedApiPackages))
        {
            RunDotnet(root, "add", csprojPath, "package", package);
        }

        return (apiCsprojRelative, context);
    }

    private static (string ApiCsprojRelative, GenerationContext Context) ScaffoldCleanArchitecture(string root, string solutionName, string adapterName, IFrameworkAdapter adapter, IPersistenceGenerator persistence)
    {
        var apiRelative = $"src/{solutionName}.Api";
        var applicationRelative = $"src/{solutionName}.Application";
        var domainRelative = $"src/{solutionName}.Domain";
        var infrastructureRelative = $"src/{solutionName}.Infrastructure";

        var context = GenerationContext.ForCleanArchitecture(solutionName, apiRelative, applicationRelative, domainRelative, infrastructureRelative);

        var domainDir = Path.Combine(root, domainRelative);
        Directory.CreateDirectory(domainDir);
        var domainCsprojPath = Path.Combine(domainDir, $"{solutionName}.Domain.csproj");
        File.WriteAllText(domainCsprojPath, ProjectFiles.ClassLibrary());

        var applicationDir = Path.Combine(root, applicationRelative);
        Directory.CreateDirectory(applicationDir);
        var applicationCsprojPath = Path.Combine(applicationDir, $"{solutionName}.Application.csproj");
        File.WriteAllText(applicationCsprojPath, ProjectFiles.ClassLibrary(new[] { $"../{solutionName}.Domain/{solutionName}.Domain.csproj" }));
        File.WriteAllText(Path.Combine(applicationDir, $"{ProgramCsBuilder.ApplicationAssemblyMarkerName}.cs"), ProgramCsBuilder.BuildApplicationAssemblyMarker(context));

        var infrastructureDir = Path.Combine(root, infrastructureRelative);
        Directory.CreateDirectory(infrastructureDir);
        var infrastructureCsprojPath = Path.Combine(infrastructureDir, $"{solutionName}.Infrastructure.csproj");
        File.WriteAllText(infrastructureCsprojPath, ProjectFiles.ClassLibrary(new[]
        {
            $"../{solutionName}.Application/{solutionName}.Application.csproj",
            $"../{solutionName}.Domain/{solutionName}.Domain.csproj",
        }));

        var apiDir = Path.Combine(root, apiRelative);
        Directory.CreateDirectory(apiDir);
        var apiCsprojPath = Path.Combine(apiDir, $"{solutionName}.Api.csproj");
        File.WriteAllText(apiCsprojPath, ProjectFiles.WebProject(new[]
        {
            $"../{solutionName}.Application/{solutionName}.Application.csproj",
            $"../{solutionName}.Infrastructure/{solutionName}.Infrastructure.csproj",
        }));
        WriteApiProjectFiles(apiDir, context, adapterName, persistence, PersistenceRegistry.ParseProvider(persistence.Name));

        foreach (var (relative, csprojPath) in new[]
        {
            (domainRelative, domainCsprojPath),
            (applicationRelative, applicationCsprojPath),
            (infrastructureRelative, infrastructureCsprojPath),
            (apiRelative, apiCsprojPath),
        })
        {
            RunDotnet(root, "sln", "add", Path.Combine(relative, Path.GetFileName(csprojPath)));
        }

        foreach (var package in adapter.RequiredPackages.Concat(persistence.ApplicationRequiredPackages))
        {
            RunDotnet(root, "add", applicationCsprojPath, "package", package);
        }

        foreach (var package in persistence.RequiredPackages)
        {
            RunDotnet(root, "add", infrastructureCsprojPath, "package", package);
        }

        foreach (var package in SharedApiPackages)
        {
            RunDotnet(root, "add", apiCsprojPath, "package", package);
        }

        return (Path.Combine(apiRelative, $"{solutionName}.Api.csproj"), context);
    }

    /// <summary>Packages every Api project needs regardless of adapter/persistence/layout: Swagger + structured logging.</summary>
    private static readonly string[] SharedApiPackages = { "Swashbuckle.AspNetCore", "Serilog.AspNetCore", "Serilog.Sinks.Console" };

    private static void WriteApiProjectFiles(string apiProjectDir, GenerationContext context, string adapterName, IPersistenceGenerator persistence, PersistenceProvider persistenceProvider)
    {
        File.WriteAllText(Path.Combine(apiProjectDir, "Program.cs"), ProgramCsBuilder.BuildProgramCs(context, adapterName, persistence));

        var commonDir = Path.Combine(apiProjectDir, "Common");
        Directory.CreateDirectory(commonDir);
        File.WriteAllText(Path.Combine(commonDir, "IEndpointDefinition.cs"), ProgramCsBuilder.BuildEndpointDefinitionInterface(context));
        File.WriteAllText(Path.Combine(commonDir, "ExceptionHandlingMiddleware.cs"), ProgramCsBuilder.BuildExceptionHandlingMiddleware(context));

        var propertiesDir = Path.Combine(apiProjectDir, "Properties");
        Directory.CreateDirectory(propertiesDir);
        File.WriteAllText(Path.Combine(propertiesDir, "launchSettings.json"), InfrastructureFiles.LaunchSettings());

        File.WriteAllText(Path.Combine(apiProjectDir, "appsettings.json"), InfrastructureFiles.AppSettings(persistenceProvider));
        File.WriteAllText(Path.Combine(apiProjectDir, "appsettings.Development.json"), InfrastructureFiles.AppSettingsDevelopment(context.RootNamespace, persistenceProvider));
    }

    internal static void RunDotnet(string workingDirectory, params string[] arguments)
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
