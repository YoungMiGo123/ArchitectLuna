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
/// <see cref="SolutionLayout.CleanArchitecture"/> (Api/Application/Domain/Infrastructure/Contracts
/// as five real projects, dependency rule pointing inward) — both get the full production
/// foundation (<see cref="FoundationFiles"/>): Result pattern, BaseEntity, user-context and
/// date/time abstractions, correlation-ID + exception middleware, Serilog request logging,
/// Swagger, health checks, DI/endpoint/middleware extension methods, Docker, docs, and test
/// projects, so there's nothing left to bolt on by hand before the first `generate` run.
/// </summary>
public static class SolutionScaffolder
{
    public static string Scaffold(string parentDirectory, string solutionName, string adapterName, string persistenceName = "in-memory", SolutionLayout layout = SolutionLayout.CleanArchitecture, bool format = true, DatabaseApplyMode applyMode = DatabaseApplyMode.Manual)
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
            ? ScaffoldCleanArchitecture(root, solutionName, adapterName, adapter, persistence, applyMode)
            : ScaffoldVerticalSlice(root, solutionName, adapterName, adapter, persistence, applyMode);

        // The production foundation: Result pattern, BaseEntity, abstractions, middleware, and
        // the extension methods Program.cs is built from. Scaffold-time only — `generate` never
        // rewrites these.
        WriteGeneratedFiles(root, FoundationFiles.BuildAll(context, adapterName, persistence, applyMode));

        // So a fresh scaffold compiles before the first `generate`: AddInfrastructure already
        // references the DbContext/store type as soon as persistence is configured, so it must
        // already exist (with zero DbSets — `generate` re-renders it with real ones once entities
        // are added).
        WriteGeneratedFiles(root, persistence.GenerateSolutionPersistence(context, Array.Empty<EntityReference>()));

        File.WriteAllText(Path.Combine(root, "Dockerfile"), InfrastructureFiles.Dockerfile(solutionName));
        File.WriteAllText(Path.Combine(root, "docker-compose.yml"), InfrastructureFiles.DockerCompose(solutionName, persistenceProvider));
        File.WriteAllText(Path.Combine(root, ".gitignore"), InfrastructureFiles.GitIgnoreContent);
        File.WriteAllText(Path.Combine(root, ".editorconfig"), InfrastructureFiles.EditorConfig());
        File.WriteAllText(Path.Combine(root, "README.md"), InfrastructureFiles.ReadMe(solutionName, adapterName, persistenceName, layout));

        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "docs", "architecture.md"), InfrastructureFiles.ArchitectureDoc(solutionName, adapterName, persistenceName, layout));
        File.WriteAllText(Path.Combine(root, "docs", "local-development.md"), InfrastructureFiles.LocalDevelopmentDoc(solutionName, persistenceProvider));

        Directory.CreateDirectory(Path.Combine(root, ".architect"));
        var model = new ArchitectModel
        {
            SolutionName = solutionName,
            Namespace = solutionName,
            Adapter = adapterName,
            Persistence = persistenceProvider,
            Database = new DatabaseSettings { ApplyMode = applyMode },
            Layout = layout,
            Features = new List<FeatureModel>(),
        };
        ModelSerializer.Save(Path.Combine(root, ".architect", "model.yaml"), model);
        ManifestStore.Save(Path.Combine(root, ".architect", "manifest.json"), new GenerationManifest());

        if (layout == SolutionLayout.CleanArchitecture)
        {
            TestProjectScaffolder.CreateApplicationTests(root, solutionName, Path.Combine("src", $"{solutionName}.Application", $"{solutionName}.Application.csproj"));
            TestProjectScaffolder.CreateInfrastructureTests(root, solutionName, Path.Combine("src", $"{solutionName}.Infrastructure", $"{solutionName}.Infrastructure.csproj"));
        }

        TestProjectScaffolder.CreateApiTests(root, solutionName, apiCsprojRelative);

        if (format)
        {
            TryRunDotnetFormat(root, solutionName);
        }

        return root;
    }

    /// <summary>
    /// Best-effort `dotnet format` over the whole solution. Never throws: a formatting failure
    /// (e.g. `dotnet format` unavailable, or a transient restore issue) must not fail scaffolding
    /// or generation, since the files it would have reformatted are already valid, buildable
    /// output — formatting is a polish step, not a correctness gate.
    /// </summary>
    internal static void TryRunDotnetFormat(string root, string solutionName)
    {
        try
        {
            RunDotnet(root, "format", $"{solutionName}.sln", "--verbosity", "quiet");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: 'dotnet format' failed and was skipped: {ex.Message}");
        }
    }

    private static void WriteGeneratedFiles(string root, IReadOnlyList<GeneratedFile> files)
    {
        foreach (var file in files)
        {
            var fullPath = Path.Combine(root, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, file.Content);
        }
    }

    private static (string ApiCsprojRelative, GenerationContext Context) ScaffoldVerticalSlice(string root, string solutionName, string adapterName, IFrameworkAdapter adapter, IPersistenceGenerator persistence, DatabaseApplyMode applyMode)
    {
        var apiProjectRelative = Path.Combine("src", $"{solutionName}.Api");
        var apiProjectDir = Path.Combine(root, apiProjectRelative);
        Directory.CreateDirectory(apiProjectDir);

        var context = GenerationContext.ForVerticalSlice(solutionName, $"src/{solutionName}.Api");

        var csprojPath = Path.Combine(apiProjectDir, $"{solutionName}.Api.csproj");
        File.WriteAllText(csprojPath, ProjectFiles.WebProject());
        WriteApiProjectFiles(apiProjectDir, context, adapterName, PersistenceRegistry.ParseProvider(persistence.Name), persistence, applyMode);

        var apiCsprojRelative = Path.Combine(apiProjectRelative, $"{solutionName}.Api.csproj");
        RunDotnet(root, "sln", "add", apiCsprojRelative);

        foreach (var package in adapter.RequiredPackages.Concat(persistence.RequiredPackages).Concat(SharedApiPackages))
        {
            RunDotnet(root, "add", csprojPath, "package", package);
        }

        return (apiCsprojRelative, context);
    }

    private static (string ApiCsprojRelative, GenerationContext Context) ScaffoldCleanArchitecture(string root, string solutionName, string adapterName, IFrameworkAdapter adapter, IPersistenceGenerator persistence, DatabaseApplyMode applyMode)
    {
        var apiRelative = $"src/{solutionName}.Api";
        var applicationRelative = $"src/{solutionName}.Application";
        var domainRelative = $"src/{solutionName}.Domain";
        var infrastructureRelative = $"src/{solutionName}.Infrastructure";
        var contractsRelative = $"src/{solutionName}.Contracts";

        var context = GenerationContext.ForCleanArchitecture(solutionName, apiRelative, applicationRelative, domainRelative, infrastructureRelative, contractsRelative);

        var domainDir = Path.Combine(root, domainRelative);
        Directory.CreateDirectory(domainDir);
        var domainCsprojPath = Path.Combine(domainDir, $"{solutionName}.Domain.csproj");
        File.WriteAllText(domainCsprojPath, ProjectFiles.ClassLibrary());

        var contractsDir = Path.Combine(root, contractsRelative);
        Directory.CreateDirectory(contractsDir);
        var contractsCsprojPath = Path.Combine(contractsDir, $"{solutionName}.Contracts.csproj");
        File.WriteAllText(contractsCsprojPath, ProjectFiles.ClassLibrary());

        var applicationDir = Path.Combine(root, applicationRelative);
        Directory.CreateDirectory(applicationDir);
        var applicationCsprojPath = Path.Combine(applicationDir, $"{solutionName}.Application.csproj");
        File.WriteAllText(applicationCsprojPath, ProjectFiles.ClassLibrary(new[]
        {
            $"../{solutionName}.Domain/{solutionName}.Domain.csproj",
            $"../{solutionName}.Contracts/{solutionName}.Contracts.csproj",
        }));

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
            $"../{solutionName}.Contracts/{solutionName}.Contracts.csproj",
        }));
        WriteApiProjectFiles(apiDir, context, adapterName, PersistenceRegistry.ParseProvider(persistence.Name), persistence, applyMode);

        foreach (var (relative, csprojPath) in new[]
        {
            (domainRelative, domainCsprojPath),
            (contractsRelative, contractsCsprojPath),
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

        // Class libraries don't get the web SDK's implicit framework reference, so the generated
        // AddInfrastructure extension (IServiceCollection/IConfiguration) needs the abstractions
        // explicitly — persistence packages happen to bring them for EF Core/Marten, but the
        // `none`/`in-memory` providers reference no packages at all.
        foreach (var package in persistence.RequiredPackages.Concat(ClassLibraryExtensionPackages))
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

    /// <summary>What a plain class library needs to declare IServiceCollection/IConfiguration extension methods.</summary>
    private static readonly string[] ClassLibraryExtensionPackages =
    {
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Configuration.Abstractions",
    };

    private static void WriteApiProjectFiles(string apiProjectDir, GenerationContext context, string adapterName, PersistenceProvider persistenceProvider, IPersistenceGenerator persistence, DatabaseApplyMode applyMode)
    {
        File.WriteAllText(Path.Combine(apiProjectDir, "Program.cs"), ProgramCsBuilder.BuildProgramCs(context, adapterName, persistence, applyMode));

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
