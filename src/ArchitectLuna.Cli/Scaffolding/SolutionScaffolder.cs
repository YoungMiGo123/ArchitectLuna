using System.Diagnostics;
using ArchitectLuna.Cli.Adapters;
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
    public static string Scaffold(string parentDirectory, string solutionName, string adapterName)
    {
        var root = Path.Combine(parentDirectory, solutionName);
        if (Directory.Exists(root))
        {
            throw new InvalidOperationException($"Directory '{root}' already exists.");
        }

        var adapter = AdapterRegistry.Resolve(adapterName);

        Directory.CreateDirectory(root);
        RunDotnet(root, "new", "sln", "-n", solutionName);

        var apiProjectRelative = Path.Combine("src", $"{solutionName}.Api");
        var apiProjectDir = Path.Combine(root, apiProjectRelative);
        Directory.CreateDirectory(apiProjectDir);

        var csprojPath = Path.Combine(apiProjectDir, $"{solutionName}.Api.csproj");
        File.WriteAllText(csprojPath, BuildCsproj());
        File.WriteAllText(Path.Combine(apiProjectDir, "Program.cs"), BuildProgramCs(solutionName, adapterName));

        var commonDir = Path.Combine(apiProjectDir, "Common");
        Directory.CreateDirectory(commonDir);
        File.WriteAllText(Path.Combine(commonDir, "IEndpointDefinition.cs"), BuildEndpointDefinitionInterface(solutionName));

        RunDotnet(root, "sln", "add", Path.Combine(apiProjectRelative, $"{solutionName}.Api.csproj"));

        foreach (var package in adapter.RequiredPackages)
        {
            RunDotnet(root, "add", csprojPath, "package", package);
        }

        Directory.CreateDirectory(Path.Combine(root, ".architect"));
        var model = new ArchitectModel
        {
            SolutionName = solutionName,
            Namespace = solutionName,
            Adapter = adapterName,
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

    private static string BuildProgramCs(string solutionName, string adapterName) => adapterName switch
    {
        "mediatr" =>
            $$"""
            using FluentValidation;
            using MediatR;
            using {{solutionName}}.Common;

            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
            builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

            var app = builder.Build();

            foreach (var endpointType in typeof(Program).Assembly.GetTypes()
                .Where(t => typeof(IEndpointDefinition).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))
            {
                var endpoint = (IEndpointDefinition)Activator.CreateInstance(endpointType)!;
                endpoint.Map(app);
            }

            app.Run();
            """,
        "wolverine" =>
            $$"""
            using FluentValidation;
            using Wolverine;
            using {{solutionName}}.Common;

            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseWolverine();
            builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

            var app = builder.Build();

            foreach (var endpointType in typeof(Program).Assembly.GetTypes()
                .Where(t => typeof(IEndpointDefinition).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))
            {
                var endpoint = (IEndpointDefinition)Activator.CreateInstance(endpointType)!;
                endpoint.Map(app);
            }

            app.Run();
            """,
        _ => throw new InvalidOperationException($"Unknown adapter '{adapterName}'."),
    };

    private static string BuildEndpointDefinitionInterface(string solutionName) =>
        $$"""
        namespace {{solutionName}}.Common;

        public interface IEndpointDefinition
        {
            void Map(IEndpointRouteBuilder app);
        }
        """;

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
