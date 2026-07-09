namespace ArchitectLuna.Cli.Scaffolding;

/// <summary>
/// Scaffolds the xUnit test project(s) every generated solution ships with — never left as a
/// "mission for later". Vertical slice gets one project (Api.Tests, since there's only one source
/// project to test); Clean Architecture gets two (Application.Tests for handler/validator unit
/// tests, Api.Tests for endpoint-level integration tests via WebApplicationFactory).
/// </summary>
public static class TestProjectScaffolder
{
    public static void CreateApiTests(string root, string solutionName, string apiCsprojRelativePath)
    {
        var projectDir = Path.Combine(root, "tests", $"{solutionName}.Api.Tests");
        Directory.CreateDirectory(projectDir);

        var csprojPath = Path.Combine(projectDir, $"{solutionName}.Api.Tests.csproj");
        var reference = RelativePosix(Path.Combine(root, apiCsprojRelativePath), projectDir);
        File.WriteAllText(csprojPath, ProjectFiles.TestProject(new[] { reference }));

        File.WriteAllText(Path.Combine(projectDir, "HealthCheckTests.cs"), BuildHealthCheckTests(solutionName));

        SolutionScaffolder.RunDotnet(root, "sln", "add", Path.Combine("tests", $"{solutionName}.Api.Tests", $"{solutionName}.Api.Tests.csproj"));
        foreach (var package in new[] { "Microsoft.NET.Test.Sdk", "xunit", "xunit.runner.visualstudio", "Microsoft.AspNetCore.Mvc.Testing" })
        {
            SolutionScaffolder.RunDotnet(root, "add", csprojPath, "package", package);
        }
    }

    public static void CreateApplicationTests(string root, string solutionName, string applicationCsprojRelativePath)
    {
        var projectDir = Path.Combine(root, "tests", $"{solutionName}.Application.Tests");
        Directory.CreateDirectory(projectDir);

        var csprojPath = Path.Combine(projectDir, $"{solutionName}.Application.Tests.csproj");
        var reference = RelativePosix(Path.Combine(root, applicationCsprojRelativePath), projectDir);
        File.WriteAllText(csprojPath, ProjectFiles.TestProject(new[] { reference }));

        File.WriteAllText(Path.Combine(projectDir, "SmokeTests.cs"), BuildApplicationSmokeTests(solutionName));

        SolutionScaffolder.RunDotnet(root, "sln", "add", Path.Combine("tests", $"{solutionName}.Application.Tests", $"{solutionName}.Application.Tests.csproj"));
        foreach (var package in new[] { "Microsoft.NET.Test.Sdk", "xunit", "xunit.runner.visualstudio" })
        {
            SolutionScaffolder.RunDotnet(root, "add", csprojPath, "package", package);
        }
    }

    private static string BuildHealthCheckTests(string solutionName) =>
        $$"""
        using System.Net;
        using Microsoft.AspNetCore.Mvc.Testing;
        using Xunit;

        namespace {{solutionName}}.Api.Tests;

        public sealed class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
        {
            private readonly WebApplicationFactory<Program> _factory;

            public HealthCheckTests(WebApplicationFactory<Program> factory)
            {
                _factory = factory;
            }

            [Fact]
            public async Task HealthEndpoint_ReturnsOk()
            {
                var client = _factory.CreateClient();
                var response = await client.GetAsync("/health");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }
        """;

    private static string BuildApplicationSmokeTests(string solutionName) =>
        $$"""
        using Xunit;

        namespace {{solutionName}}.Application.Tests;

        public sealed class SmokeTests
        {
            [Fact]
            public void ApplicationAssembly_Loads()
            {
                Assert.NotNull(typeof({{solutionName}}.Application.ApplicationAssemblyMarker));
            }
        }
        """;

    /// <summary>Relative path from <paramref name="fromDir"/> to <paramref name="toFile"/>, always with forward slashes (MSBuild accepts either, but this keeps generated .csproj content deterministic across OSes).</summary>
    private static string RelativePosix(string toFile, string fromDir) =>
        Path.GetRelativePath(fromDir, toFile).Replace('\\', '/');
}
