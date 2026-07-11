using ArchitectLuna.Cli.Scaffolding;
using ArchitectLuna.Core.Model;
using ArchitectLuna.Template.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.Template.Tests.VerticalSlice;

/// <summary>
/// docs/requirements/004-standards-return-types.md §17: the foundation itself must differ by
/// <see cref="ApiStyle"/> — Controllers solutions get MVC registration, controller routing, and
/// the IActionResult-returning ResultActionExtensions; Minimal API solutions get none of that (and
/// vice versa for the IEndpointDefinition reflection scan). A solution has exactly one api-style,
/// so these are mutually exclusive, not additive.
/// </summary>
public sealed class ControllersFoundationTests
{
    private const string Api = "src/BillingService.Api";

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Controllers_Foundation_IncludesResultActionExtensions(string adapter)
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), adapter, ApiStyle.Controllers);
        var paths = files.Select(f => f.RelativePath).ToHashSet();

        Assert.Contains($"{Api}/Results/ResultActionExtensions.cs", paths);
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void MinimalApi_Foundation_DoesNotIncludeResultActionExtensions(string adapter)
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), adapter, ApiStyle.MinimalApi);
        var paths = files.Select(f => f.RelativePath).ToHashSet();

        Assert.DoesNotContain($"{Api}/Results/ResultActionExtensions.cs", paths);

        // Default overload (no explicit ApiStyle) must behave the same as MinimalApi.
        var defaultFiles = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), adapter);
        Assert.DoesNotContain($"{Api}/Results/ResultActionExtensions.cs", defaultFiles.Select(f => f.RelativePath));
    }

    [Fact]
    public void ResultActionExtensions_MirrorsResultExtensionsButReturnsIActionResult()
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), "mediatr", ApiStyle.Controllers);
        var content = GenerationTestHarness.ContentOf(files, $"{Api}/Results/ResultActionExtensions.cs");

        Assert.Contains("public static IActionResult ToOkActionResponse<TValue, TResponse>", content);
        Assert.Contains("public static IActionResult ToCreatedActionResponse<TValue, TResponse>", content);
        Assert.Contains("public static IActionResult ToNoContentActionResponse<TValue>", content);
        Assert.Contains("public static IActionResult ToErrorActionResponse(this Result result)", content);
        Assert.Contains("public static IActionResult ToValidationActionErrorResponse(this FluentValidation.Results.ValidationResult validationResult)", content);
        Assert.Contains("ApiResponse.Success(map(result.Value))", content);
        Assert.Contains("ApiResponse.Failure<object?>(apiError)", content);
    }

    [Theory]
    [InlineData(ApiStyle.Controllers, true)]
    [InlineData(ApiStyle.MinimalApi, false)]
    public void ApiDependencyInjection_RegistersControllersOnlyForControllersStyle(ApiStyle apiStyle, bool expectRegistration)
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), "mediatr", apiStyle);
        var content = GenerationTestHarness.ContentOf(files, $"{Api}/ApiDependencyInjection.cs");

        Assert.Equal(expectRegistration, content.Contains("services.AddControllers();"));
    }

    [Theory]
    [InlineData(ApiStyle.Controllers, true, false)]
    [InlineData(ApiStyle.MinimalApi, false, true)]
    public void EndpointExtensions_MapsControllersOrScansIEndpointDefinition_ButNotBoth(ApiStyle apiStyle, bool expectMapControllers, bool expectReflectionScan)
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), "mediatr", apiStyle);
        var content = GenerationTestHarness.ContentOf(files, $"{Api}/Common/EndpointExtensions.cs");

        Assert.Equal(expectMapControllers, content.Contains("app.MapControllers();"));
        Assert.Equal(expectReflectionScan, content.Contains("typeof(IEndpointDefinition).IsAssignableFrom(t)"));
    }
}
