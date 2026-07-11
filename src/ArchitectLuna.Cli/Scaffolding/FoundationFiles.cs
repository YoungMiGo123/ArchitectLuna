using ArchitectLuna.Core.Generation;
using ArchitectLuna.Core.Model;

namespace ArchitectLuna.Cli.Scaffolding;

/// <summary>
/// Builds the production foundation every scaffolded solution ships with (see
/// docs/requirements/001-implementation-architecture.md): the Result pattern, BaseEntity, user
/// context and date/time abstractions, correlation-ID + exception middleware, and the extension
/// methods (`AddApi`/`AddApplication`/`AddInfrastructure`/`UseApiMiddleware`/`MapApiEndpoints`/
/// `UseApiLogging`) that keep Program.cs small. Everything is a pure function from
/// <see cref="GenerationContext"/> (+ adapter/persistence choice) to <see cref="GeneratedFile"/>
/// records, so ArchitectLuna.Template.Tests can verify the full foundation without touching the
/// file system. Files are placed via the context's project targets, which is what makes one code
/// path serve both layouts: under vertical slice everything lands in the one Api project; under
/// Clean Architecture each file lands in the project that owns its concern.
/// </summary>
public static class FoundationFiles
{
    /// <summary>
    /// Every foundation file for one scaffolded solution. Scaffold-time only — `generate` never
    /// rewrites these. Persistence *registration* (AddPersistence, schema init, DB health check) is
    /// not here: the provider emits it from GenerateSolutionPersistence so it is regenerated with
    /// full entity knowledge; this only wires the stable `AddInfrastructure` that calls into it.
    /// </summary>
    public static IReadOnlyList<GeneratedFile> BuildAll(GenerationContext context, string adapterName)
    {
        var files = new List<GeneratedFile>
        {
            // Application-owned: Result pattern + abstractions the handlers depend on.
            new($"{context.Application.ProjectRoot}/Common/Results/Result.cs", BuildResult(context)),
            new($"{context.Application.ProjectRoot}/Common/Results/Error.cs", BuildError(context)),
            new($"{context.Application.ProjectRoot}/Common/Results/ValidationError.cs", BuildValidationError(context)),
            new($"{context.Application.ProjectRoot}/Common/Results/PagedResult.cs", BuildPagedResult(context)),
            new($"{context.Application.ProjectRoot}/Common/Abstractions/IDateTimeProvider.cs", BuildDateTimeProviderInterface(context)),
            new($"{context.Application.ProjectRoot}/Common/Abstractions/IUserContext.cs", BuildUserContextInterface(context)),
            new($"{context.Application.ProjectRoot}/ApplicationDependencyInjection.cs", BuildApplicationDependencyInjection(context, adapterName)),

            // Domain-owned: the base model every generated entity/document inherits.
            new($"{context.Domain.ProjectRoot}/Common/BaseEntity.cs", BuildBaseEntity(context)),

            // Infrastructure-owned: technical implementations + persistence registration.
            new($"{context.Infrastructure.ProjectRoot}/Services/SystemDateTimeProvider.cs", BuildSystemDateTimeProvider(context)),
            new($"{context.Infrastructure.ProjectRoot}/InfrastructureDependencyInjection.cs", BuildInfrastructureDependencyInjection(context)),

            // Api-owned: HTTP concerns.
            new($"{context.Api.ProjectRoot}/Common/IEndpointDefinition.cs", BuildEndpointDefinitionInterface(context)),
            new($"{context.Api.ProjectRoot}/Common/ExceptionHandlingMiddleware.cs", BuildExceptionHandlingMiddleware(context)),
            new($"{context.Api.ProjectRoot}/Common/CorrelationIdMiddleware.cs", BuildCorrelationIdMiddleware(context)),
            new($"{context.Api.ProjectRoot}/Common/ResultHttpExtensions.cs", BuildResultHttpExtensions(context)),
            new($"{context.Api.ProjectRoot}/Common/MiddlewareExtensions.cs", BuildMiddlewareExtensions(context)),
            new($"{context.Api.ProjectRoot}/Common/EndpointExtensions.cs", BuildEndpointExtensions(context)),
            new($"{context.Api.ProjectRoot}/Common/LoggingExtensions.cs", BuildLoggingExtensions(context)),
            new($"{context.Api.ProjectRoot}/Services/HttpUserContext.cs", BuildHttpUserContext(context)),
            new($"{context.Api.ProjectRoot}/ApiDependencyInjection.cs", BuildApiDependencyInjection(context)),
        };

        return files;
    }

    public static string BuildResult(GenerationContext context) =>
        $$"""
        namespace {{context.Application.RootNamespace}}.Common.Results;

        /// <summary>
        /// Explicit success/failure outcome for handlers — normal business outcomes (not found,
        /// conflict, validation failure) are values, not exceptions. The API layer translates a
        /// failed result to the matching HTTP status code.
        /// </summary>
        public class Result
        {
            protected Result(bool isSuccess, Error? error)
            {
                if (isSuccess && error is not null)
                {
                    throw new InvalidOperationException("A successful result cannot carry an error.");
                }

                if (!isSuccess && error is null)
                {
                    throw new InvalidOperationException("A failed result must carry an error.");
                }

                IsSuccess = isSuccess;
                Error = error;
            }

            public bool IsSuccess { get; }

            public bool IsFailure => !IsSuccess;

            public Error? Error { get; }

            public static Result Success() => new(true, null);

            public static Result Failure(Error error) => new(false, error);
        }

        public sealed class Result<T> : Result
        {
            private readonly T? _value;

            private Result(bool isSuccess, T? value, Error? error)
                : base(isSuccess, error)
            {
                _value = value;
            }

            /// <summary>The success value. Throws when accessed on a failed result — check <see cref="Result.IsSuccess"/> first.</summary>
            public T Value => IsSuccess
                ? _value!
                : throw new InvalidOperationException("Cannot access the value of a failed result.");

            public static Result<T> Success(T value) => new(true, value, null);

            public static new Result<T> Failure(Error error) => new(false, default, error);
        }
        """;

    public static string BuildError(GenerationContext context) =>
        $$"""
        namespace {{context.Application.RootNamespace}}.Common.Results;

        public enum ErrorType
        {
            Validation,
            NotFound,
            Conflict,
            Unauthorized,
            Forbidden,
            Unexpected,
        }

        public record Error(ErrorType Type, string Code, string Message)
        {
            public static Error Validation(string message) => new(ErrorType.Validation, "validation_failed", message);

            public static Error NotFound(string message) => new(ErrorType.NotFound, "not_found", message);

            public static Error Conflict(string message) => new(ErrorType.Conflict, "conflict", message);

            public static Error Unauthorized(string message) => new(ErrorType.Unauthorized, "unauthorized", message);

            public static Error Forbidden(string message) => new(ErrorType.Forbidden, "forbidden", message);

            public static Error Unexpected(string message) => new(ErrorType.Unexpected, "unexpected", message);
        }
        """;

    public static string BuildValidationError(GenerationContext context) =>
        $$"""
        namespace {{context.Application.RootNamespace}}.Common.Results;

        /// <summary>A validation failure carrying the per-field messages, for handler-level validation outcomes.</summary>
        public sealed record ValidationError : Error
        {
            public ValidationError(IReadOnlyDictionary<string, string[]> failures)
                : base(ErrorType.Validation, "validation_failed", "One or more validation failures occurred.")
            {
                Failures = failures;
            }

            public IReadOnlyDictionary<string, string[]> Failures { get; }
        }
        """;

    public static string BuildPagedResult(GenerationContext context) =>
        $$"""
        namespace {{context.Application.RootNamespace}}.Common.Results;

        public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount)
        {
            public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

            public bool HasNextPage => Page < TotalPages;

            public bool HasPreviousPage => Page > 1;
        }
        """;

    public static string BuildDateTimeProviderInterface(GenerationContext context) =>
        $$"""
        namespace {{context.Application.RootNamespace}}.Common.Abstractions;

        /// <summary>Injectable clock so application code never calls DateTime.UtcNow directly (testability).</summary>
        public interface IDateTimeProvider
        {
            DateTime UtcNow { get; }
        }
        """;

    public static string BuildUserContextInterface(GenerationContext context) =>
        $$"""
        namespace {{context.Application.RootNamespace}}.Common.Abstractions;

        /// <summary>
        /// Who is performing the current action. Application code depends on this abstraction —
        /// never on HttpContext directly; the API layer provides the HTTP-backed implementation.
        /// </summary>
        public interface IUserContext
        {
            string? UserId { get; }

            string? Email { get; }

            IReadOnlyList<string> Roles { get; }

            string? CorrelationId { get; }
        }
        """;

    public static string BuildApplicationDependencyInjection(GenerationContext context, string adapterName)
    {
        var registrations = adapterName switch
        {
            // Wolverine's handler discovery is host-level (UseWolverine in Program.cs), so only
            // the validators register here; MediatR is a plain IServiceCollection registration.
            "mediatr" =>
                "        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ApplicationDependencyInjection).Assembly));\n" +
                "        services.AddValidatorsFromAssembly(typeof(ApplicationDependencyInjection).Assembly);",
            "wolverine" =>
                "        services.AddValidatorsFromAssembly(typeof(ApplicationDependencyInjection).Assembly);",
            _ => throw new InvalidOperationException($"Unknown adapter '{adapterName}'."),
        };

        return $$"""
        using FluentValidation;
        using Microsoft.Extensions.DependencyInjection;

        namespace {{context.Application.RootNamespace}};

        /// <summary>
        /// Registers everything the Application layer owns: the dispatcher's handler discovery and
        /// the FluentValidation validators. This class is also the Application assembly's anchor
        /// type for assembly scanning.
        /// </summary>
        public static class ApplicationDependencyInjection
        {
            public static IServiceCollection AddApplication(this IServiceCollection services)
            {
        {{registrations}}
                return services;
            }
        }
        """;
    }

    public static string BuildBaseEntity(GenerationContext context) =>
        $$"""
        namespace {{context.Domain.RootNamespace}}.Common;

        /// <summary>
        /// Common production fields shared by every generated entity. Audit fields are declared
        /// here so persistence and application code have one consistent shape; populating them is
        /// business logic and belongs in handlers/interceptors.
        /// </summary>
        public abstract class BaseEntity
        {
            public Guid Id { get; set; }

            public DateTime CreatedAt { get; set; }

            public string? CreatedBy { get; set; }

            public DateTime? UpdatedAt { get; set; }

            public string? UpdatedBy { get; set; }

            public bool IsDeleted { get; set; }
        }
        """;

    public static string BuildSystemDateTimeProvider(GenerationContext context) =>
        $$"""
        using {{context.Application.RootNamespace}}.Common.Abstractions;

        namespace {{context.Infrastructure.RootNamespace}}.Services;

        public sealed class SystemDateTimeProvider : IDateTimeProvider
        {
            public DateTime UtcNow => DateTime.UtcNow;
        }
        """;

    public static string BuildInfrastructureDependencyInjection(GenerationContext context) =>
        $$"""
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.DependencyInjection;
        using {{context.Application.RootNamespace}}.Common.Abstractions;
        using {{context.Infrastructure.RootNamespace}}.Services;

        namespace {{context.Infrastructure.RootNamespace}};

        /// <summary>
        /// Registers the technical implementations this solution's Infrastructure owns: the
        /// date/time clock and — via the provider-generated <c>AddPersistence</c> extension (see
        /// PersistenceRegistration.cs, regenerated on every `generate`) — the persistence provider,
        /// its startup schema initialization, and its database health check.
        /// </summary>
        public static class InfrastructureDependencyInjection
        {
            public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
            {
                services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
                services.AddPersistence(configuration);
                return services;
            }
        }
        """;

    public static string BuildEndpointDefinitionInterface(GenerationContext context) =>
        $$"""
        namespace {{context.Api.RootNamespace}}.Common;

        /// <summary>
        /// Shared endpoint definition pattern: every generated endpoint implements this and is
        /// discovered/mapped by MapApiEndpoints(), so adding a feature never edits Program.cs.
        /// </summary>
        public interface IEndpointDefinition
        {
            void Map(IEndpointRouteBuilder app);
        }
        """;

    public static string BuildExceptionHandlingMiddleware(GenerationContext context) =>
        $$"""
        using FluentValidation;

        namespace {{context.Api.RootNamespace}}.Common;

        /// <summary>
        /// Last-resort translation of exceptions to consistent problem+json responses. Normal
        /// business outcomes flow through the Result pattern, not exceptions — this middleware
        /// covers hand-written code that throws: FluentValidation's ValidationException becomes
        /// 400, KeyNotFoundException 404, UnauthorizedAccessException 403, a concurrency conflict
        /// (matched by type name, not a hard EF Core reference — see below) becomes 409, any other
        /// database update failure is logged and returned as a safe 500, and anything else
        /// unhandled is logged and returned as a safe, generic 500.
        ///
        /// EF Core's DbUpdateException/DbUpdateConcurrencyException are matched by
        /// <c>ex.GetType().Name</c> instead of a `catch (DbUpdateException ex)` clause: the Api
        /// project doesn't otherwise reference EF Core (that's an Infrastructure/Application
        /// concern — see docs/ARCHITECTURE.md's layout section), and this middleware is the same
        /// file for every persistence provider, so it can't special-case EF without either an
        /// unwanted package reference or generating a second variant of this file.
        /// </summary>
        public sealed class ExceptionHandlingMiddleware
        {
            private readonly RequestDelegate _next;
            private readonly ILogger<ExceptionHandlingMiddleware> _logger;

            public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
            {
                _next = next;
                _logger = logger;
            }

            public async Task InvokeAsync(HttpContext context)
            {
                try
                {
                    await _next(context);
                }
                catch (ValidationException ex)
                {
                    var failures = ex.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/problem+json";
                    await context.Response.WriteAsJsonAsync(new { status = StatusCodes.Status400BadRequest, title = "Validation failed.", errors = failures });
                }
                catch (KeyNotFoundException ex)
                {
                    await WriteProblem(context, StatusCodes.Status404NotFound, ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    await WriteProblem(context, StatusCodes.Status403Forbidden, ex.Message);
                }
                catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
                {
                    await WriteProblem(context, StatusCodes.Status409Conflict, "The record was modified or deleted by another process.");
                }
                catch (Exception ex) when (ex.GetType().Name == "DbUpdateException")
                {
                    _logger.LogError(ex, "Database update failed for {Method} {Path}", context.Request.Method, context.Request.Path);
                    await WriteProblem(context, StatusCodes.Status500InternalServerError, "A database error occurred.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
                    await WriteProblem(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
                }
            }

            private static Task WriteProblem(HttpContext context, int statusCode, string message)
            {
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/problem+json";
                return context.Response.WriteAsJsonAsync(new { status = statusCode, title = message });
            }
        }
        """;

    public static string BuildCorrelationIdMiddleware(GenerationContext context) =>
        $$"""
        using Serilog.Context;

        namespace {{context.Api.RootNamespace}}.Common;

        /// <summary>
        /// Accepts an incoming X-Correlation-ID header (or mints one), echoes it on the response,
        /// exposes it via HttpContext.Items for <c>IUserContext.CorrelationId</c>, and pushes it
        /// into the Serilog log context so every log line for the request carries it.
        /// </summary>
        public sealed class CorrelationIdMiddleware
        {
            public const string HeaderName = "X-Correlation-ID";

            private readonly RequestDelegate _next;

            public CorrelationIdMiddleware(RequestDelegate next)
            {
                _next = next;
            }

            public async Task InvokeAsync(HttpContext context)
            {
                var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var incoming) && !string.IsNullOrWhiteSpace(incoming)
                    ? incoming.ToString()
                    : Guid.NewGuid().ToString();

                context.Items[HeaderName] = correlationId;
                context.Response.Headers[HeaderName] = correlationId;

                using (LogContext.PushProperty("CorrelationId", correlationId))
                {
                    await _next(context);
                }
            }
        }
        """;

    public static string BuildResultHttpExtensions(GenerationContext context) =>
        $$"""
        using {{context.Application.RootNamespace}}.Common.Results;

        namespace {{context.Api.RootNamespace}}.Common;

        /// <summary>
        /// The one place a failed <c>Result</c> becomes an HTTP response, so every endpoint maps
        /// errors identically: Validation→400, NotFound→404, Conflict→409, Unauthorized→401,
        /// Forbidden→403, Unexpected→500.
        /// </summary>
        public static class ResultHttpExtensions
        {
            public static IResult ToProblem(this Result result)
            {
                if (result.IsSuccess)
                {
                    throw new InvalidOperationException("Cannot convert a successful result to a problem response.");
                }

                var error = result.Error!;
                var statusCode = error.Type switch
                {
                    ErrorType.Validation => StatusCodes.Status400BadRequest,
                    ErrorType.NotFound => StatusCodes.Status404NotFound,
                    ErrorType.Conflict => StatusCodes.Status409Conflict,
                    ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                    ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                    _ => StatusCodes.Status500InternalServerError,
                };

                // Fully qualified: this class's own namespace has a sibling "Results" *namespace*
                // (the Result pattern types), which shadows ASP.NET's Results class here.
                return Microsoft.AspNetCore.Http.Results.Problem(title: error.Code, detail: error.Message, statusCode: statusCode);
            }
        }
        """;

    public static string BuildMiddlewareExtensions(GenerationContext context) =>
        $$"""
        using Serilog;

        namespace {{context.Api.RootNamespace}}.Common;

        public static class MiddlewareExtensions
        {
            public static WebApplication UseApiMiddleware(this WebApplication app)
            {
                app.UseMiddleware<CorrelationIdMiddleware>();
                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.UseSerilogRequestLogging();

                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }

                return app;
            }
        }
        """;

    public static string BuildEndpointExtensions(GenerationContext context) =>
        $$"""
        using Microsoft.AspNetCore.Diagnostics.HealthChecks;

        namespace {{context.Api.RootNamespace}}.Common;

        public static class EndpointExtensions
        {
            /// <summary>
            /// Maps the health probes plus every discovered <see cref="IEndpointDefinition"/> — adding
            /// a feature never edits Program.cs. Liveness (<c>/health</c>) reports the process is up;
            /// readiness (<c>/health/ready</c>) runs the DB-tagged checks so an orchestrator can tell
            /// "started" from "can actually serve traffic".
            /// </summary>
            public static WebApplication MapApiEndpoints(this WebApplication app)
            {
                app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
                app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

                foreach (var endpointType in typeof(EndpointExtensions).Assembly.GetTypes()
                    .Where(t => typeof(IEndpointDefinition).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false }))
                {
                    var endpoint = (IEndpointDefinition)Activator.CreateInstance(endpointType)!;
                    endpoint.Map(app);
                }

                return app;
            }
        }
        """;

    public static string BuildLoggingExtensions(GenerationContext context) =>
        $$"""
        using Serilog;

        namespace {{context.Api.RootNamespace}}.Common;

        public static class LoggingExtensions
        {
            /// <summary>Structured console logging via Serilog, configured from appsettings + DI-registered enrichers.</summary>
            public static IHostBuilder UseApiLogging(this IHostBuilder host, IConfiguration configuration)
            {
                host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console());

                return host;
            }
        }
        """;

    public static string BuildHttpUserContext(GenerationContext context) =>
        $$"""
        using System.Security.Claims;
        using {{context.Api.RootNamespace}}.Common;
        using {{context.Application.RootNamespace}}.Common.Abstractions;

        namespace {{context.Api.RootNamespace}}.Services;

        /// <summary>HTTP-backed <see cref="IUserContext"/> — the Application layer only ever sees the abstraction.</summary>
        public sealed class HttpUserContext : IUserContext
        {
            private readonly IHttpContextAccessor _httpContextAccessor;

            public HttpUserContext(IHttpContextAccessor httpContextAccessor)
            {
                _httpContextAccessor = httpContextAccessor;
            }

            public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

            public string? Email => User?.FindFirstValue(ClaimTypes.Email);

            public IReadOnlyList<string> Roles =>
                User?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList() ?? new List<string>();

            public string? CorrelationId =>
                _httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.HeaderName] as string;

            private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;
        }
        """;

    public static string BuildApiDependencyInjection(GenerationContext context) =>
        $$"""
        using {{context.Application.RootNamespace}}.Common.Abstractions;
        using {{context.Api.RootNamespace}}.Services;

        namespace {{context.Api.RootNamespace}};

        /// <summary>Registers the HTTP-facing services this Api project owns.</summary>
        public static class ApiDependencyInjection
        {
            public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
            {
                services.AddEndpointsApiExplorer();
                services.AddSwaggerGen();
                services.AddHealthChecks();
                services.AddHttpContextAccessor();
                services.AddScoped<IUserContext, HttpUserContext>();
                return services;
            }
        }
        """;
}
