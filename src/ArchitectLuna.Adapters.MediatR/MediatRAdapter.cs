using ArchitectLuna.Core.Generation;
using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Naming;
using ArchitectLuna.Templates;
using ArchitectLuna.Templates.RenderModels;

namespace ArchitectLuna.Adapters.MediatR;

/// <summary>
/// Endpoints, validators, Request/Response DTOs, Result records, and mappings are rendered from
/// the templates shared with every other adapter (see Templates/Shared) — HTTP mapping always
/// goes through the same minimal-API IEndpointDefinition pattern regardless of backend. Only the
/// Message/Handler shape is MediatR-specific here (IRequest/IRequestHandler), because that's an
/// unavoidable framework requirement.
///
/// File placement follows the <see cref="GenerationContext"/> targets: Message/Result/Handler/
/// Validator/Mappings files go to <see cref="GenerationContext.Application"/>; Request/Response
/// DTOs go to a `Contracts/` subfolder of that same operation slice (never a separate project —
/// see <see cref="GenerationContext"/>'s doc comment); Endpoint files go to
/// <see cref="GenerationContext.Api"/>. For vertical slice these are all the same project, so
/// every file lands in the feature slice; for Clean Architecture Application and Api are
/// genuinely separate projects.
///
/// Handlers return Result&lt;T&gt;; endpoints translate failures via ToProblem() and map success
/// to 201 (Create), 200 (Update/queries), or 204 (Delete). Handler bodies come from the
/// configured <see cref="IPersistenceGenerator"/> — defaults to
/// <see cref="NullPersistenceGenerator"/> (the original NotImplementedException placeholder) when
/// none is supplied, so existing callers are unaffected.
/// </summary>
public sealed class MediatRAdapter : IFrameworkAdapter
{
    private const string AdapterKey = "mediatr";
    private const string DispatcherUsing = "MediatR";
    private const string DispatcherType = "ISender";
    private const string DispatcherParam = "sender";

    /// <summary>
    /// Appended to a paged query's message fields (never to <see cref="QueryModel.Params"/> —
    /// see <see cref="QueryModel.IsPaged"/>'s doc comment). Missing from the query string, they
    /// bind to 0; the persistence generator's paged handler body clamps that to a real default.
    /// </summary>
    private static readonly IReadOnlyList<MessageFieldRenderModel> PagingFields = new[]
    {
        new MessageFieldRenderModel { Name = "Page", Type = "int" },
        new MessageFieldRenderModel { Name = "PageSize", Type = "int" },
    };

    private readonly TemplateEngine _templateEngine = new();
    private readonly EmbeddedTemplateProvider _templateProvider = new();
    private readonly IPersistenceGenerator _persistence;

    public MediatRAdapter(IPersistenceGenerator? persistence = null)
    {
        _persistence = persistence ?? new NullPersistenceGenerator();
    }

    public string Name => AdapterKey;

    public IReadOnlyList<string> RequiredPackages { get; } = new[]
    {
        "MediatR",
        "FluentValidation",
        "FluentValidation.DependencyInjectionExtensions",
    };

    public IReadOnlyList<GeneratedFile> GenerateCommand(GenerationContext context, FeatureModel feature, CommandModel command)
    {
        var names = new SliceNames(command.Name, "Command");
        var slice = new SlicePaths(context, feature.Name, command.Name);
        var route = RouteInference.CommandRoute(feature, command);
        var resultsNamespace = $"{context.Application.RootNamespace}.Common.Results";
        var wrappedResultType = $"Result<{names.Result}>";

        var fields = command.Fields.Select(f => new MessageFieldRenderModel { Name = f.Name, Type = f.Type }).ToList();
        var resultFields = new List<MessageFieldRenderModel> { new() { Name = "Id", Type = "Guid" } };

        var binding = BindCommand(context, feature, command);

        var messageModel = new MessageRenderModel
        {
            Namespace = slice.ApplicationNamespace,
            ResultsNamespace = resultsNamespace,
            MessageName = names.Message,
            HandlerName = names.Handler,
            ResultName = names.Result,
            ResultType = wrappedResultType,
            Fields = fields,
            ResultFields = resultFields,
            HandlerBody = binding.Body,
            HandlerUsings = binding.Usings.Concat(new[] { resultsNamespace }).Distinct().ToList(),
            HasHandlerDependency = binding.DependencyType is not null,
            HandlerDependencyType = binding.DependencyType,
            HandlerDependencyParam = binding.DependencyParam,
        };

        var hasRouteId = command.Kind is CommandKind.Update or CommandKind.Delete;
        var hasBody = command.Kind is CommandKind.Create or CommandKind.Update;
        var hasResponse = command.Kind is not CommandKind.Delete;
        var httpMapMethod = command.Kind switch
        {
            CommandKind.Update => "MapPut",
            CommandKind.Delete => "MapDelete",
            _ => "MapPost",
        };

        var dispatchCall = command.Kind == CommandKind.Delete
            ? $"{DispatcherParam}.Send(new {names.Message}(id), cancellationToken)"
            : $"{DispatcherParam}.Send(command, cancellationToken)";

        var successExpression = command.Kind switch
        {
            CommandKind.Create => $"Results.Created($\"{route}/{{result.Value.Id}}\", result.Value.ToResponse())",
            CommandKind.Update => "Results.Ok(result.Value.ToResponse())",
            _ => "Results.NoContent()",
        };

        var endpointModel = new CommandEndpointRenderModel
        {
            Namespace = slice.EndpointNamespace,
            ApiRootNamespace = context.Api.RootNamespace,
            MessageNamespace = slice.ApplicationNamespace,
            MessageName = names.Message,
            EndpointName = names.Endpoint,
            ResultType = wrappedResultType,
            Route = route,
            HttpMapMethod = httpMapMethod,
            HasRouteId = hasRouteId,
            HasBody = hasBody,
            DispatcherUsing = DispatcherUsing,
            DispatcherType = DispatcherType,
            DispatcherParam = DispatcherParam,
            DispatchCall = dispatchCall,
            ResultsNamespace = resultsNamespace,
            RequestName = hasBody ? names.Request : null,
            ContractsNamespace = slice.ContractsNamespace,
            HasContractsUsing = (hasBody || hasResponse) && slice.ContractsNamespace != slice.ApplicationNamespace,
            SuccessExpression = successExpression,
        };

        var files = new List<GeneratedFile>
        {
            new($"{slice.ApplicationPath}/{names.Message}.cs", RenderAdapter("Message.cs.sbn", messageModel)),
            new($"{slice.ApplicationPath}/{names.Result}.cs", RenderShared("Record.cs.sbn", new RecordRenderModel
            {
                Namespace = slice.ApplicationNamespace,
                RecordName = names.Result,
                Fields = resultFields,
            })),
            new($"{slice.ApplicationPath}/{names.Handler}.cs", RenderAdapter("Handler.cs.sbn", messageModel)),
        };

        if (hasBody)
        {
            var requestFields = command.Kind == CommandKind.Update
                ? fields.Where(f => f.Name != "Id").ToList()
                : fields;
            files.Add(new GeneratedFile($"{slice.ContractsPath}/{names.Request}.cs", RenderShared("Record.cs.sbn", new RecordRenderModel
            {
                Namespace = slice.ContractsNamespace,
                RecordName = names.Request,
                Fields = requestFields,
            })));

            var validatorModel = new ValidatorRenderModel
            {
                Namespace = slice.ApplicationNamespace,
                MessageName = names.Message,
                ValidatorName = names.Validator,
                Fields = command.Fields.Select(f => new ValidatorFieldRenderModel
                {
                    Name = f.Name,
                    Rules = DefaultValidationRules.DefaultsFor(f).Concat(f.Rules).Distinct().ToList(),
                }).ToList(),
            };
            files.Add(new GeneratedFile($"{slice.ApplicationPath}/{names.Validator}.cs", RenderShared("Validator.cs.sbn", validatorModel)));
        }

        if (hasResponse)
        {
            files.Add(new GeneratedFile($"{slice.ContractsPath}/{names.Response}.cs", RenderShared("Record.cs.sbn", new RecordRenderModel
            {
                Namespace = slice.ContractsNamespace,
                RecordName = names.Response,
                Fields = resultFields,
            })));
        }

        if (hasBody || hasResponse)
        {
            files.Add(new GeneratedFile($"{slice.ApplicationPath}/{names.Mappings}.cs", RenderShared("Mappings.cs.sbn", new MappingsRenderModel
            {
                Namespace = slice.ApplicationNamespace,
                ContractsNamespace = slice.ContractsNamespace,
                NeedsContractsUsing = slice.ContractsNamespace != slice.ApplicationNamespace,
                MappingsName = names.Mappings,
                HasRequest = hasBody,
                RequestName = names.Request,
                MessageName = names.Message,
                RequestTakesId = command.Kind == CommandKind.Update,
                ToCommandArgs = string.Join(", ", command.Fields.Select(f =>
                    command.Kind == CommandKind.Update && f.Name == "Id" ? "id" : $"request.{f.Name}")),
                HasResponse = hasResponse,
                ResultName = names.Result,
                ResponseName = names.Response,
                ToResponseArgs = string.Join(", ", resultFields.Select(f => $"result.{f.Name}")),
            })));
        }

        files.Add(new GeneratedFile($"{slice.EndpointPath}/{names.Endpoint}.cs", RenderShared("CommandEndpoint.cs.sbn", endpointModel)));

        return files;
    }

    public IReadOnlyList<GeneratedFile> GenerateQuery(GenerationContext context, FeatureModel feature, QueryModel query)
    {
        var names = new SliceNames(query.Name, "Query");
        var slice = new SlicePaths(context, feature.Name, query.Name);
        var route = RouteInference.QueryRoute(feature, query);
        var resultsNamespace = $"{context.Application.RootNamespace}.Common.Results";

        var parameters = query.Params.Select(p => new MessageFieldRenderModel { Name = p.Name, Type = p.Type }).ToList();
        var resultFields = query.ResultFields.Count > 0
            ? query.ResultFields.Select(f => new MessageFieldRenderModel { Name = f.Name, Type = f.Type }).ToList()
            : parameters;

        if (query.IsPaged)
        {
            parameters = parameters.Concat(PagingFields).ToList();
        }

        var wrappedResultType = query.IsPaged
            ? $"Result<PagedResult<{names.Result}>>"
            : query.IsCollection
                ? $"Result<IReadOnlyList<{names.Result}>>"
                : $"Result<{names.Result}>";

        var binding = BindQuery(context, feature, query);

        var messageModel = new MessageRenderModel
        {
            Namespace = slice.ApplicationNamespace,
            ResultsNamespace = resultsNamespace,
            MessageName = names.Message,
            HandlerName = names.Handler,
            ResultName = names.Result,
            ResultType = wrappedResultType,
            Fields = parameters,
            ResultFields = resultFields,
            HandlerBody = binding.Body,
            HandlerUsings = binding.Usings.Concat(new[] { resultsNamespace }).Distinct().ToList(),
            HasHandlerDependency = binding.DependencyType is not null,
            HandlerDependencyType = binding.DependencyType,
            HandlerDependencyParam = binding.DependencyParam,
        };

        var isSingleRouteParam = !query.IsPaged && query.Params.Count == 1 && query.Params[0].Name.EndsWith("Id", StringComparison.Ordinal);
        var isZeroParam = !query.IsPaged && query.Params.Count == 0;
        var routeParamName = isSingleRouteParam ? NamingConventions.ToCamelCase(query.Params[0].Name) : null;

        var dispatchCall = isSingleRouteParam
            ? $"{DispatcherParam}.Send(new {names.Message}({routeParamName}), cancellationToken)"
            : isZeroParam
                ? $"{DispatcherParam}.Send(new {names.Message}(), cancellationToken)"
                : $"{DispatcherParam}.Send(query, cancellationToken)";

        var successExpression = query.IsPaged
            ? "Results.Ok(new { items = result.Value.Items.Select(item => item.ToResponse()).ToList(), result.Value.Page, result.Value.PageSize, result.Value.TotalCount, result.Value.TotalPages, result.Value.HasNextPage, result.Value.HasPreviousPage })"
            : query.IsCollection
                ? "Results.Ok(result.Value.Select(item => item.ToResponse()).ToList())"
                : "Results.Ok(result.Value.ToResponse())";

        var endpointModel = new QueryEndpointRenderModel
        {
            Namespace = slice.EndpointNamespace,
            ApiRootNamespace = context.Api.RootNamespace,
            MessageNamespace = slice.ApplicationNamespace,
            MessageName = names.Message,
            EndpointName = names.Endpoint,
            ResultType = wrappedResultType,
            Route = route,
            Params = parameters,
            IsSingleRouteParam = isSingleRouteParam,
            IsZeroParam = isZeroParam,
            RouteParamName = routeParamName,
            RouteParamType = isSingleRouteParam ? query.Params[0].Type : null,
            DispatcherUsing = DispatcherUsing,
            DispatcherType = DispatcherType,
            DispatcherParam = DispatcherParam,
            DispatchCall = dispatchCall,
            ResultsNamespace = resultsNamespace,
            ContractsNamespace = slice.ContractsNamespace,
            HasContractsUsing = slice.ContractsNamespace != slice.ApplicationNamespace,
            SuccessExpression = successExpression,
        };

        return new[]
        {
            new GeneratedFile($"{slice.ApplicationPath}/{names.Message}.cs", RenderAdapter("Message.cs.sbn", messageModel)),
            new GeneratedFile($"{slice.ApplicationPath}/{names.Result}.cs", RenderShared("Record.cs.sbn", new RecordRenderModel
            {
                Namespace = slice.ApplicationNamespace,
                RecordName = names.Result,
                Fields = resultFields,
            })),
            new GeneratedFile($"{slice.ApplicationPath}/{names.Handler}.cs", RenderAdapter("Handler.cs.sbn", messageModel)),
            new GeneratedFile($"{slice.ContractsPath}/{names.Response}.cs", RenderShared("Record.cs.sbn", new RecordRenderModel
            {
                Namespace = slice.ContractsNamespace,
                RecordName = names.Response,
                Fields = resultFields,
            })),
            new GeneratedFile($"{slice.ApplicationPath}/{names.Mappings}.cs", RenderShared("Mappings.cs.sbn", new MappingsRenderModel
            {
                Namespace = slice.ApplicationNamespace,
                ContractsNamespace = slice.ContractsNamespace,
                NeedsContractsUsing = slice.ContractsNamespace != slice.ApplicationNamespace,
                MappingsName = names.Mappings,
                HasRequest = false,
                HasResponse = true,
                ResultName = names.Result,
                ResponseName = names.Response,
                ToResponseArgs = string.Join(", ", resultFields.Select(f => $"result.{f.Name}")),
            })),
            new GeneratedFile($"{slice.EndpointPath}/{names.Endpoint}.cs", RenderShared("QueryEndpoint.cs.sbn", endpointModel)),
        };
    }

    /// <summary>Standard file/type names derived from one operation name (message suffix is "Command" or "Query").</summary>
    private readonly record struct SliceNames(string OperationName, string MessageSuffix)
    {
        public string Message => $"{OperationName}{MessageSuffix}";
        public string Handler => $"{OperationName}Handler";
        public string Validator => $"{OperationName}Validator";
        public string Endpoint => $"{OperationName}Endpoint";
        public string Result => $"{OperationName}Result";
        public string Request => $"{OperationName}Request";
        public string Response => $"{OperationName}Response";
        public string Mappings => $"{OperationName}Mappings";
    }

    /// <summary>
    /// Where one operation's files live: Application slice, Api slice, and a `Contracts/`
    /// subfolder of the Application slice (docs/requirements/003-improvements.md §2.2-2.3 — DTOs
    /// live inside the owning slice, never in a separate project).
    /// </summary>
    private readonly record struct SlicePaths
    {
        public SlicePaths(GenerationContext context, string featureName, string operationName)
        {
            ApplicationNamespace = $"{context.Application.RootNamespace}.Features.{featureName}.{operationName}";
            ApplicationPath = $"{context.Application.ProjectRoot}/Features/{featureName}/{operationName}";
            EndpointNamespace = $"{context.Api.RootNamespace}.Features.{featureName}.{operationName}";
            EndpointPath = $"{context.Api.ProjectRoot}/Features/{featureName}/{operationName}";
            ContractsNamespace = $"{ApplicationNamespace}.Contracts";
            ContractsPath = $"{ApplicationPath}/Contracts";
        }

        public string ApplicationNamespace { get; }
        public string ApplicationPath { get; }
        public string EndpointNamespace { get; }
        public string EndpointPath { get; }
        public string ContractsNamespace { get; }
        public string ContractsPath { get; }
    }

    private HandlerBinding BindCommand(GenerationContext context, FeatureModel feature, CommandModel command)
    {
        var entity = command.EntityName is null ? null : feature.Entities.FirstOrDefault(e => e.Name == command.EntityName);
        return entity is null ? HandlerBinding.NotImplemented() : _persistence.BindCommandHandler(context, feature, entity, command);
    }

    private HandlerBinding BindQuery(GenerationContext context, FeatureModel feature, QueryModel query)
    {
        var entity = query.EntityName is null ? null : feature.Entities.FirstOrDefault(e => e.Name == query.EntityName);
        return entity is null ? HandlerBinding.NotImplemented() : _persistence.BindQueryHandler(context, feature, entity, query);
    }

    private string RenderAdapter(string templateFileName, object model) => Render(AdapterKey, templateFileName, model);

    private string RenderShared(string templateFileName, object model) => Render("Shared", templateFileName, model);

    private string Render(string folder, string templateFileName, object model)
    {
        var templateText = _templateProvider.GetTemplate(folder, templateFileName);
        return _templateEngine.Render(templateText, $"{folder}/{templateFileName}", model);
    }
}
