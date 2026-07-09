using ArchitectLuna.Cli.Scaffolding;
using ArchitectLuna.Template.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.Template.Tests.CleanArchitecture;

/// <summary>
/// docs/requirements/002-testing-layer.md §Production Foundation Tests, clean-architecture
/// profile: the same foundation as vertical slice, but each file must land in the project that
/// owns its concern — Result pattern and abstractions in Application, BaseEntity in Domain,
/// implementations in Infrastructure, HTTP concerns in Api.
/// </summary>
public sealed class ProductionFoundationTests
{
    private const string Api = "src/BillingService.Api";
    private const string Application = "src/BillingService.Application";
    private const string Domain = "src/BillingService.Domain";
    private const string Infrastructure = "src/BillingService.Infrastructure";

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Foundation_PlacesEachFileInTheOwningProject(string adapter)
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.CleanArchitectureContext(), adapter);
        var paths = files.Select(f => f.RelativePath).ToHashSet();

        var expected = new[]
        {
            $"{Application}/Common/Results/Result.cs",
            $"{Application}/Common/Results/Error.cs",
            $"{Application}/Common/Results/ValidationError.cs",
            $"{Application}/Common/Results/PagedResult.cs",
            $"{Application}/Common/Abstractions/IDateTimeProvider.cs",
            $"{Application}/Common/Abstractions/IUserContext.cs",
            $"{Application}/ApplicationDependencyInjection.cs",
            $"{Domain}/Common/BaseEntity.cs",
            $"{Infrastructure}/Services/SystemDateTimeProvider.cs",
            $"{Infrastructure}/InfrastructureDependencyInjection.cs",
            $"{Api}/Common/IEndpointDefinition.cs",
            $"{Api}/Common/ExceptionHandlingMiddleware.cs",
            $"{Api}/Common/CorrelationIdMiddleware.cs",
            $"{Api}/Common/ResultHttpExtensions.cs",
            $"{Api}/Common/MiddlewareExtensions.cs",
            $"{Api}/Common/EndpointExtensions.cs",
            $"{Api}/Common/LoggingExtensions.cs",
            $"{Api}/Services/HttpUserContext.cs",
            $"{Api}/ApiDependencyInjection.cs",
        };

        Assert.All(expected, path => Assert.Contains(path, paths));
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void DomainFiles_StayFreeOfDispatcherAndPersistenceConcerns(string adapter)
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.CleanArchitectureContext(), adapter);

        foreach (var file in files.Where(f => f.RelativePath.StartsWith(Domain, StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("MediatR", file.Content);
            Assert.DoesNotContain("Wolverine", file.Content);
            Assert.DoesNotContain("EntityFrameworkCore", file.Content);
            Assert.DoesNotContain("Marten", file.Content);
        }
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void ProgramCs_KeepsTheCleanExtensionShape(string adapter)
    {
        var program = ProgramCsBuilder.BuildProgramCs(GenerationTestHarness.CleanArchitectureContext(), adapter);

        Assert.Contains("builder.Host.UseApiLogging(builder.Configuration);", program);
        Assert.Contains(".AddApi(builder.Configuration)", program);
        Assert.Contains(".AddApplication()", program);
        Assert.Contains(".AddInfrastructure(builder.Configuration);", program);
        Assert.Contains("app.UseApiMiddleware();", program);
        Assert.Contains("app.MapApiEndpoints();", program);
        Assert.DoesNotContain("AddSwaggerGen", program);
        Assert.DoesNotContain("AddDbContext", program);
    }

    [Fact]
    public void ProgramCs_Wolverine_DiscoversTheApplicationAssembly()
    {
        var program = ProgramCsBuilder.BuildProgramCs(GenerationTestHarness.CleanArchitectureContext(), "wolverine");

        Assert.Contains("opts.UseRuntimeCompilation();", program);
        Assert.Contains("opts.Discovery.IncludeAssembly(typeof(ApplicationDependencyInjection).Assembly);", program);
    }

    [Fact]
    public void AddInfrastructure_RegistersTheClockAndDelegatesToAddPersistence()
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.CleanArchitectureContext(), "mediatr");
        var content = GenerationTestHarness.ContentOf(files, $"{Infrastructure}/InfrastructureDependencyInjection.cs");

        Assert.Contains("services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();", content);
        Assert.Contains("services.AddPersistence(configuration);", content);
    }

    [Fact]
    public void EfCore_AddPersistence_WiresTheApplicationOwnedInterfaceToTheConcreteDbContext()
    {
        var files = GenerationTestHarness.PersistenceSolutionFiles(GenerationTestHarness.CleanArchitectureContext(), "efcore-postgres", GenerationTestHarness.InvoiceFeature());
        var content = GenerationTestHarness.ContentOf(files, $"{Infrastructure}/PersistenceRegistration.cs");

        Assert.Contains("services.AddDbContext<BillingServiceDbContext>", content);
        Assert.Contains("services.AddScoped<IBillingServiceDbContext>(sp => sp.GetRequiredService<BillingServiceDbContext>())", content);
        Assert.Contains("services.AddHostedService<DatabaseInitializer>();", content);
    }

    [Fact]
    public void Marten_AddPersistence_RegistersEachDocumentTypeInInfrastructure()
    {
        var files = GenerationTestHarness.PersistenceSolutionFiles(GenerationTestHarness.CleanArchitectureContext(), "marten", GenerationTestHarness.InvoiceFeature());
        var content = GenerationTestHarness.ContentOf(files, $"{Infrastructure}/PersistenceRegistration.cs");

        Assert.Contains("options.RegisterDocumentType<Invoice>();", content);
        Assert.Contains("ApplyAllDatabaseChangesOnStartup()", content);
    }
}
