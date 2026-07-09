using System.Text;
using ArchitectLuna.Core.Generation;

namespace ArchitectLuna.Cli.Scaffolding;

/// <summary>
/// Builds the Api project's Program.cs and its "Common" support files (IEndpointDefinition,
/// ExceptionHandlingMiddleware). One code path handles both layouts: for vertical slice,
/// <see cref="GenerationContext.Application"/> and <see cref="GenerationContext.Api"/> are the same
/// project, so the extra Application-assembly scan is a harmless no-op duplicate; for Clean
/// Architecture it's what makes MediatR/Wolverine/FluentValidation actually find the
/// handlers/validators that <c>generate</c> places in the Application project instead of the Api
/// project that owns Program.cs.
/// </summary>
public static class ProgramCsBuilder
{
    public const string ApplicationAssemblyMarkerName = "ApplicationAssemblyMarker";

    public static string BuildApplicationAssemblyMarker(GenerationContext context) =>
        $$"""
        namespace {{context.Application.RootNamespace}};

        /// <summary>
        /// Anchor type so the Api's composition root (Program.cs) can point MediatR/Wolverine
        /// handler discovery and FluentValidation registration at this assembly by type, without
        /// needing a hardcoded assembly name string.
        /// </summary>
        public sealed class {{ApplicationAssemblyMarkerName}}
        {
        }
        """;

    public static string BuildEndpointDefinitionInterface(GenerationContext context) =>
        $$"""
        namespace {{context.Api.RootNamespace}}.Common;

        public interface IEndpointDefinition
        {
            void Map(IEndpointRouteBuilder app);
        }
        """;

    public static string BuildExceptionHandlingMiddleware(GenerationContext context) =>
        $$"""
        namespace {{context.Api.RootNamespace}}.Common;

        /// <summary>
        /// Maps exceptions thrown by generated handlers to proper HTTP status codes instead of
        /// letting them surface as an unhandled 500: generated Get/Update/Delete handlers throw
        /// <see cref="KeyNotFoundException"/> for a missing id, which becomes 404.
        /// </summary>
        public sealed class ExceptionHandlingMiddleware
        {
            private readonly RequestDelegate _next;

            public ExceptionHandlingMiddleware(RequestDelegate next)
            {
                _next = next;
            }

            public async Task InvokeAsync(HttpContext context)
            {
                try
                {
                    await _next(context);
                }
                catch (KeyNotFoundException ex)
                {
                    await WriteProblem(context, StatusCodes.Status404NotFound, ex.Message);
                }
                catch (Exception ex)
                {
                    await WriteProblem(context, StatusCodes.Status500InternalServerError, ex.Message);
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

    public static string BuildProgramCs(GenerationContext context, string adapterName, IPersistenceGenerator persistence)
    {
        var (adapterUsings, bootstrapLines) = BuildAdapterBootstrap(context, adapterName);

        var usings = new List<string> { "Serilog" };
        usings.AddRange(adapterUsings);
        usings.AddRange(persistence.ProgramCsUsings);
        usings.Add($"{context.Api.RootNamespace}.Common");
        if (context.HasSeparateInfrastructure)
        {
            usings.Add(context.Application.RootNamespace);
        }

        var bodyLines = new List<string>
        {
            "builder.Services.AddEndpointsApiExplorer();",
            "builder.Services.AddSwaggerGen();",
            "builder.Services.AddHealthChecks();",
            string.Empty,
        };
        bodyLines.AddRange(bootstrapLines);
        bodyLines.AddRange(persistence.BuildProgramCsRegistration(context));

        var sb = new StringBuilder();
        foreach (var u in usings.Distinct())
        {
            sb.AppendLine($"using {u};");
        }

        sb.AppendLine();
        sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
        sb.AppendLine();
        sb.AppendLine("builder.Host.UseSerilog((context, services, configuration) => configuration");
        sb.AppendLine("    .ReadFrom.Configuration(context.Configuration)");
        sb.AppendLine("    .ReadFrom.Services(services)");
        sb.AppendLine("    .Enrich.FromLogContext()");
        sb.AppendLine("    .WriteTo.Console());");
        sb.AppendLine();
        foreach (var line in bodyLines)
        {
            sb.AppendLine(line);
        }

        sb.AppendLine();
        sb.AppendLine("var app = builder.Build();");
        sb.AppendLine();
        sb.AppendLine("if (app.Environment.IsDevelopment())");
        sb.AppendLine("{");
        sb.AppendLine("    app.UseSwagger();");
        sb.AppendLine("    app.UseSwaggerUI();");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("app.UseMiddleware<ExceptionHandlingMiddleware>();");
        sb.AppendLine();
        sb.AppendLine("app.MapHealthChecks(\"/health\");");
        sb.AppendLine();
        sb.AppendLine("foreach (var endpointType in typeof(Program).Assembly.GetTypes()");
        sb.AppendLine("    .Where(t => typeof(IEndpointDefinition).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))");
        sb.AppendLine("{");
        sb.AppendLine("    var endpoint = (IEndpointDefinition)Activator.CreateInstance(endpointType)!;");
        sb.AppendLine("    endpoint.Map(app);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("app.Run();");
        sb.AppendLine();
        sb.AppendLine("// Exposed for WebApplicationFactory<Program> in the Api test project.");
        sb.Append("public partial class Program { }");
        return sb.ToString();
    }

    private static (IReadOnlyList<string> Usings, IReadOnlyList<string> BootstrapLines) BuildAdapterBootstrap(GenerationContext context, string adapterName)
    {
        var assemblyExpressions = context.HasSeparateInfrastructure
            ? new[] { "typeof(Program).Assembly", $"typeof({ApplicationAssemblyMarkerName}).Assembly" }
            : new[] { "typeof(Program).Assembly" };

        return adapterName switch
        {
            "mediatr" => (
                new[] { "FluentValidation", "MediatR" },
                BuildMediatRBootstrap(assemblyExpressions)),
            "wolverine" => (
                new[] { "FluentValidation", "Wolverine" },
                BuildWolverineBootstrap(assemblyExpressions)),
            _ => throw new InvalidOperationException($"Unknown adapter '{adapterName}'."),
        };
    }

    private static IReadOnlyList<string> BuildMediatRBootstrap(IReadOnlyList<string> assemblyExpressions)
    {
        var lines = new List<string>
        {
            $"builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies({string.Join(", ", assemblyExpressions)}));",
        };
        lines.AddRange(assemblyExpressions.Select(a => $"builder.Services.AddValidatorsFromAssembly({a});"));
        return lines;
    }

    private static IReadOnlyList<string> BuildWolverineBootstrap(IReadOnlyList<string> assemblyExpressions)
    {
        var lines = new List<string>();
        if (assemblyExpressions.Count > 1)
        {
            lines.Add("builder.Host.UseWolverine(opts =>");
            lines.Add("{");
            foreach (var extra in assemblyExpressions.Skip(1))
            {
                lines.Add($"    opts.Discovery.IncludeAssembly({extra});");
            }

            lines.Add("});");
        }
        else
        {
            lines.Add("builder.Host.UseWolverine();");
        }

        lines.AddRange(assemblyExpressions.Select(a => $"builder.Services.AddValidatorsFromAssembly({a});"));
        return lines;
    }
}
