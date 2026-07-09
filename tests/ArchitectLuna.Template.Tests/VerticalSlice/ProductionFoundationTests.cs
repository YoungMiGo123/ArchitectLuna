using ArchitectLuna.Cli.Scaffolding;
using ArchitectLuna.Core.Model;
using ArchitectLuna.Template.Tests.Infrastructure;
using Xunit;

namespace ArchitectLuna.Template.Tests.VerticalSlice;

/// <summary>
/// docs/requirements/002-testing-layer.md §Production Foundation Tests, vertical-slice profile:
/// every scaffold ships the Result pattern, BaseEntity, middleware, user-context/date-time
/// abstractions, and DI/endpoint extension methods — and Program.cs keeps the clean extension
/// style instead of accumulating raw registrations.
/// </summary>
public sealed class ProductionFoundationTests
{
    private const string Api = "src/BillingService.Api";

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void Foundation_ContainsEveryRequiredFile(string adapter)
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), adapter, GenerationTestHarness.Persistence("in-memory"));
        var paths = files.Select(f => f.RelativePath).ToHashSet();

        var expected = new[]
        {
            $"{Api}/Common/Results/Result.cs",
            $"{Api}/Common/Results/Error.cs",
            $"{Api}/Common/Results/ValidationError.cs",
            $"{Api}/Common/Results/PagedResult.cs",
            $"{Api}/Common/Abstractions/IDateTimeProvider.cs",
            $"{Api}/Common/Abstractions/IUserContext.cs",
            $"{Api}/Persistence/Common/BaseEntity.cs",
            $"{Api}/Persistence/Services/SystemDateTimeProvider.cs",
            $"{Api}/Common/IEndpointDefinition.cs",
            $"{Api}/Common/ExceptionHandlingMiddleware.cs",
            $"{Api}/Common/CorrelationIdMiddleware.cs",
            $"{Api}/Common/ResultHttpExtensions.cs",
            $"{Api}/Common/MiddlewareExtensions.cs",
            $"{Api}/Common/EndpointExtensions.cs",
            $"{Api}/Common/LoggingExtensions.cs",
            $"{Api}/Services/HttpUserContext.cs",
            $"{Api}/ApiDependencyInjection.cs",
            $"{Api}/ApplicationDependencyInjection.cs",
            $"{Api}/Persistence/InfrastructureDependencyInjection.cs",
        };

        Assert.All(expected, path => Assert.Contains(path, paths));
    }

    [Fact]
    public void ResultPattern_DeclaresAllRequiredTypes()
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), "mediatr", GenerationTestHarness.Persistence("in-memory"));

        var result = GenerationTestHarness.ContentOf(files, $"{Api}/Common/Results/Result.cs");
        Assert.Contains("public class Result", result);
        Assert.Contains("public sealed class Result<T> : Result", result);

        var error = GenerationTestHarness.ContentOf(files, $"{Api}/Common/Results/Error.cs");
        Assert.Contains("public enum ErrorType", error);
        Assert.Contains("public record Error(ErrorType Type, string Code, string Message)", error);

        Assert.Contains("public sealed record ValidationError : Error", GenerationTestHarness.ContentOf(files, $"{Api}/Common/Results/ValidationError.cs"));
        Assert.Contains("public sealed record PagedResult<T>", GenerationTestHarness.ContentOf(files, $"{Api}/Common/Results/PagedResult.cs"));
    }

    [Fact]
    public void ResultHttpExtensions_MapEveryErrorTypeToItsStatusCode()
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), "mediatr", GenerationTestHarness.Persistence("in-memory"));
        var content = GenerationTestHarness.ContentOf(files, $"{Api}/Common/ResultHttpExtensions.cs");

        Assert.Contains("ErrorType.Validation => StatusCodes.Status400BadRequest", content);
        Assert.Contains("ErrorType.NotFound => StatusCodes.Status404NotFound", content);
        Assert.Contains("ErrorType.Conflict => StatusCodes.Status409Conflict", content);
        Assert.Contains("ErrorType.Unauthorized => StatusCodes.Status401Unauthorized", content);
        Assert.Contains("ErrorType.Forbidden => StatusCodes.Status403Forbidden", content);
        Assert.Contains("StatusCodes.Status500InternalServerError", content);
    }

    [Fact]
    public void BaseEntity_CarriesProductionAuditFields()
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), "mediatr", GenerationTestHarness.Persistence("in-memory"));
        var content = GenerationTestHarness.ContentOf(files, $"{Api}/Persistence/Common/BaseEntity.cs");

        Assert.Contains("public abstract class BaseEntity", content);
        foreach (var member in new[] { "Guid Id", "DateTime CreatedAt", "string? CreatedBy", "DateTime? UpdatedAt", "string? UpdatedBy", "bool IsDeleted" })
        {
            Assert.Contains(member, content);
        }
    }

    [Theory]
    [InlineData("mediatr")]
    [InlineData("wolverine")]
    public void ProgramCs_KeepsTheCleanExtensionShape(string adapter)
    {
        var program = ProgramCsBuilder.BuildProgramCs(GenerationTestHarness.VerticalSliceContext(), adapter);

        Assert.Contains("builder.Host.UseApiLogging(builder.Configuration);", program);
        Assert.Contains(".AddApi(builder.Configuration)", program);
        Assert.Contains(".AddApplication()", program);
        Assert.Contains(".AddInfrastructure(builder.Configuration);", program);
        Assert.Contains("app.UseApiMiddleware();", program);
        Assert.Contains("app.MapApiEndpoints();", program);

        // Program.cs must not be a dumping ground — the low-level registrations live behind
        // the extension methods.
        Assert.DoesNotContain("AddSwaggerGen", program);
        Assert.DoesNotContain("AddHealthChecks", program);
        Assert.DoesNotContain("UseSerilog(", program);
        Assert.DoesNotContain("AddMediatR", program);
        Assert.DoesNotContain("AddValidatorsFromAssembly", program);
    }

    [Fact]
    public void ProgramCs_Wolverine_BootstrapsDispatcherWithRuntimeCompilation()
    {
        var program = ProgramCsBuilder.BuildProgramCs(GenerationTestHarness.VerticalSliceContext(), "wolverine");

        Assert.Contains("builder.Host.UseWolverine(", program);
        Assert.Contains("opts.UseRuntimeCompilation();", program);
    }

    [Fact]
    public void Middleware_IsRegisteredThroughExtensionMethods()
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), "mediatr", GenerationTestHarness.Persistence("in-memory"));
        var middleware = GenerationTestHarness.ContentOf(files, $"{Api}/Common/MiddlewareExtensions.cs");

        Assert.Contains("UseMiddleware<CorrelationIdMiddleware>", middleware);
        Assert.Contains("UseMiddleware<ExceptionHandlingMiddleware>", middleware);
        Assert.Contains("UseSerilogRequestLogging", middleware);

        var endpoints = GenerationTestHarness.ContentOf(files, $"{Api}/Common/EndpointExtensions.cs");
        Assert.Contains("MapHealthChecks(\"/health\")", endpoints);
        Assert.Contains("IEndpointDefinition", endpoints);
    }

    [Fact]
    public void ExceptionMiddleware_HandlesDbUpdateConcurrencyAsConflictAndDbUpdateAsLoggedFailure()
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), "mediatr", GenerationTestHarness.Persistence("in-memory"));
        var middleware = GenerationTestHarness.ContentOf(files, $"{Api}/Common/ExceptionHandlingMiddleware.cs");

        Assert.Contains("DbUpdateConcurrencyException", middleware);
        Assert.Contains("StatusCodes.Status409Conflict", middleware);
        Assert.Contains("DbUpdateException", middleware);
        Assert.Contains("Database update failed", middleware);
    }

    [Theory]
    [InlineData("mediatr", "AddMediatR", true)]
    [InlineData("mediatr", "AddValidatorsFromAssembly", true)]
    [InlineData("wolverine", "AddValidatorsFromAssembly", true)]
    [InlineData("wolverine", "AddMediatR", false)]
    public void AddApplication_RegistersTheChosenDispatchersServices(string adapter, string registration, bool expected)
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), adapter, GenerationTestHarness.Persistence("in-memory"));
        var content = GenerationTestHarness.ContentOf(files, $"{Api}/ApplicationDependencyInjection.cs");

        Assert.Equal(expected, content.Contains(registration));
    }

    [Theory]
    [InlineData("in-memory", "services.AddSingleton<BillingService.Persistence.InMemoryStore>();")]
    [InlineData("efcore-postgres", "options.UseNpgsql(configuration.GetConnectionString(\"Default\"))")]
    [InlineData("efcore-sqlserver", "options.UseSqlServer(configuration.GetConnectionString(\"Default\"))")]
    [InlineData("marten", "options.Connection(configuration.GetConnectionString(\"Default\")!);")]
    public void AddInfrastructure_RegistersTheChosenPersistenceProvider(string persistence, string expectedRegistration)
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), "mediatr", GenerationTestHarness.Persistence(persistence));
        var content = GenerationTestHarness.ContentOf(files, $"{Api}/Persistence/InfrastructureDependencyInjection.cs");

        Assert.Contains(expectedRegistration, content);
        Assert.Contains("services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();", content);
    }

    [Fact]
    public void AddInfrastructure_NonePersistence_StillRegistersFoundationServices()
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), "mediatr", GenerationTestHarness.Persistence("none"));
        var content = GenerationTestHarness.ContentOf(files, $"{Api}/Persistence/InfrastructureDependencyInjection.cs");

        Assert.Contains("services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();", content);
        Assert.DoesNotContain("AddDbContext", content);
        Assert.DoesNotContain("AddMarten", content);
    }

    [Fact]
    public void UserContext_ApplicationSeesAbstraction_ApiOwnsImplementation()
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), "mediatr", GenerationTestHarness.Persistence("in-memory"));

        var abstraction = GenerationTestHarness.ContentOf(files, $"{Api}/Common/Abstractions/IUserContext.cs");
        Assert.DoesNotContain("IHttpContextAccessor", abstraction);
        Assert.DoesNotContain("using Microsoft.AspNetCore", abstraction);

        var implementation = GenerationTestHarness.ContentOf(files, $"{Api}/Services/HttpUserContext.cs");
        Assert.Contains("IHttpContextAccessor", implementation);
        Assert.Contains(": IUserContext", implementation);

        var registration = GenerationTestHarness.ContentOf(files, $"{Api}/ApiDependencyInjection.cs");
        Assert.Contains("services.AddScoped<IUserContext, HttpUserContext>();", registration);
    }

    [Theory]
    [InlineData("efcore-postgres", "Database.Migrate()")]
    [InlineData("marten", "ApplyAllConfiguredChangesToDatabaseAsync")]
    public void OnStartupApplyMode_AddsApplyCallToProgramCs(string persistence, string expectedCall)
    {
        var provider = GenerationTestHarness.Persistence(persistence);
        var program = ProgramCsBuilder.BuildProgramCs(GenerationTestHarness.VerticalSliceContext(), "mediatr", provider, DatabaseApplyMode.OnStartup);

        Assert.Contains(expectedCall, program);
        Assert.Contains("var app = builder.Build();", program);
        // The apply call must run after the app is built (needs a resolved DI scope) but before
        // middleware/endpoints are wired up.
        Assert.True(program.IndexOf("var app = builder.Build();", StringComparison.Ordinal) < program.IndexOf(expectedCall, StringComparison.Ordinal));
        Assert.True(program.IndexOf(expectedCall, StringComparison.Ordinal) < program.IndexOf("app.UseApiMiddleware();", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("efcore-postgres")]
    [InlineData("marten")]
    [InlineData("in-memory")]
    public void ManualApplyMode_DoesNotAddApplyCallToProgramCs(string persistence)
    {
        var provider = GenerationTestHarness.Persistence(persistence);
        var program = ProgramCsBuilder.BuildProgramCs(GenerationTestHarness.VerticalSliceContext(), "mediatr", provider, DatabaseApplyMode.Manual);

        Assert.DoesNotContain("Database.Migrate()", program);
        Assert.DoesNotContain("ApplyAllConfiguredChangesToDatabaseAsync", program);
    }

    [Fact]
    public void OnStartupApplyMode_MartenSetsAutoCreateSchemaObjectsToAll()
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), "mediatr", GenerationTestHarness.Persistence("marten"), DatabaseApplyMode.OnStartup);
        var content = GenerationTestHarness.ContentOf(files, $"{Api}/Persistence/InfrastructureDependencyInjection.cs");

        Assert.Contains("options.AutoCreateSchemaObjects = AutoCreate.All;", content);
    }

    [Fact]
    public void ManualApplyMode_MartenLeavesAutoCreateSchemaObjectsAtCreateOrUpdate()
    {
        var files = FoundationFiles.BuildAll(GenerationTestHarness.VerticalSliceContext(), "mediatr", GenerationTestHarness.Persistence("marten"), DatabaseApplyMode.Manual);
        var content = GenerationTestHarness.ContentOf(files, $"{Api}/Persistence/InfrastructureDependencyInjection.cs");

        Assert.Contains("options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;", content);
    }
}
