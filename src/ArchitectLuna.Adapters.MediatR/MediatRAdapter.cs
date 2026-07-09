using ArchitectLuna.Core.Generation;
using ArchitectLuna.Core.Model;
using ArchitectLuna.Core.Naming;
using ArchitectLuna.Templates;
using ArchitectLuna.Templates.RenderModels;

namespace ArchitectLuna.Adapters.MediatR;

/// <summary>
/// Endpoints and validators are rendered from the templates shared with every other adapter (see
/// Templates/Shared) — HTTP mapping always goes through the same minimal-API IEndpointDefinition
/// pattern regardless of backend. Only the Message/Handler shape is MediatR-specific here
/// (IRequest/IRequestHandler), because that's an unavoidable framework requirement.
///
/// Handler bodies come from the configured <see cref="IPersistenceGenerator"/> — defaults to
/// <see cref="NullPersistenceGenerator"/> (the original NotImplementedException placeholder) when
/// none is supplied, so existing callers are unaffected.
/// </summary>
public sealed class MediatRAdapter : IFrameworkAdapter
{
    private const string AdapterKey = "mediatr";
    private const string DispatcherUsing = "MediatR";
    private const string DispatcherType = "ISender";
    private const string DispatcherParam = "sender";

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
        var messageName = $"{command.Name}Command";
        var handlerName = $"{command.Name}Handler";
        var validatorName = $"{command.Name}Validator";
        var endpointName = $"{command.Name}Endpoint";
        var resultName = $"{command.Name}Result";
        var ns = $"{context.RootNamespace}.Features.{feature.Name}.{command.Name}";
        var slicePath = $"{context.ProjectRelativeRoot}/Features/{feature.Name}/{command.Name}";
        var route = RouteInference.CommandRoute(feature, command);

        var fields = command.Fields.Select(f => new MessageFieldRenderModel { Name = f.Name, Type = f.Type }).ToList();
        var resultFields = new List<MessageFieldRenderModel> { new() { Name = "Id", Type = "Guid" } };

        var binding = BindCommand(context, feature, command);

        var messageModel = new MessageRenderModel
        {
            Namespace = ns,
            RootNamespace = context.RootNamespace,
            MessageName = messageName,
            HandlerName = handlerName,
            ResultName = resultName,
            ResultType = resultName,
            Fields = fields,
            ResultFields = resultFields,
            HandlerBody = binding.Body,
            HandlerUsings = binding.Usings,
            HasHandlerDependency = binding.DependencyType is not null,
            HandlerDependencyType = binding.DependencyType,
            HandlerDependencyParam = binding.DependencyParam,
        };

        var hasRouteId = command.Kind is CommandKind.Update or CommandKind.Delete;
        var hasBody = command.Kind is CommandKind.Create or CommandKind.Update;
        var httpMapMethod = command.Kind switch
        {
            CommandKind.Update => "MapPut",
            CommandKind.Delete => "MapDelete",
            _ => "MapPost",
        };

        var dispatchCall = command.Kind switch
        {
            CommandKind.Update => $"{DispatcherParam}.Send(resolvedCommand, cancellationToken)",
            CommandKind.Delete => $"{DispatcherParam}.Send(new {messageName}(id), cancellationToken)",
            _ => $"{DispatcherParam}.Send(command, cancellationToken)",
        };

        var endpointModel = new CommandEndpointRenderModel
        {
            Namespace = ns,
            RootNamespace = context.RootNamespace,
            MessageName = messageName,
            EndpointName = endpointName,
            ResultType = resultName,
            Route = route,
            HttpMapMethod = httpMapMethod,
            HasRouteId = hasRouteId,
            HasBody = hasBody,
            DispatcherUsing = DispatcherUsing,
            DispatcherType = DispatcherType,
            DispatcherParam = DispatcherParam,
            DispatchCall = dispatchCall,
        };

        var files = new List<GeneratedFile>
        {
            new($"{slicePath}/{messageName}.cs", RenderAdapter("Message.cs.sbn", messageModel)),
            new($"{slicePath}/{handlerName}.cs", RenderAdapter("Handler.cs.sbn", messageModel)),
        };

        if (hasBody)
        {
            var validatorModel = new ValidatorRenderModel
            {
                Namespace = ns,
                MessageName = messageName,
                ValidatorName = validatorName,
                Fields = command.Fields.Select(f => new ValidatorFieldRenderModel { Name = f.Name, Rules = f.Rules }).ToList(),
            };
            files.Add(new GeneratedFile($"{slicePath}/{validatorName}.cs", RenderShared("Validator.cs.sbn", validatorModel)));
        }

        files.Add(new GeneratedFile($"{slicePath}/{endpointName}.cs", RenderShared("CommandEndpoint.cs.sbn", endpointModel)));

        return files;
    }

    public IReadOnlyList<GeneratedFile> GenerateQuery(GenerationContext context, FeatureModel feature, QueryModel query)
    {
        var messageName = $"{query.Name}Query";
        var handlerName = $"{query.Name}Handler";
        var endpointName = $"{query.Name}Endpoint";
        var resultName = $"{query.Name}Result";
        var ns = $"{context.RootNamespace}.Features.{feature.Name}.{query.Name}";
        var slicePath = $"{context.ProjectRelativeRoot}/Features/{feature.Name}/{query.Name}";
        var route = RouteInference.QueryRoute(feature, query);

        var parameters = query.Params.Select(p => new MessageFieldRenderModel { Name = p.Name, Type = p.Type }).ToList();
        var resultFields = query.ResultFields.Count > 0
            ? query.ResultFields.Select(f => new MessageFieldRenderModel { Name = f.Name, Type = f.Type }).ToList()
            : parameters;

        var resultType = query.IsCollection ? $"IReadOnlyList<{resultName}>" : resultName;

        var binding = BindQuery(context, feature, query);

        var messageModel = new MessageRenderModel
        {
            Namespace = ns,
            RootNamespace = context.RootNamespace,
            MessageName = messageName,
            HandlerName = handlerName,
            ResultName = resultName,
            ResultType = resultType,
            Fields = parameters,
            ResultFields = resultFields,
            HandlerBody = binding.Body,
            HandlerUsings = binding.Usings,
            HasHandlerDependency = binding.DependencyType is not null,
            HandlerDependencyType = binding.DependencyType,
            HandlerDependencyParam = binding.DependencyParam,
        };

        var isSingleRouteParam = query.Params.Count == 1 && query.Params[0].Name.EndsWith("Id", StringComparison.Ordinal);
        var isZeroParam = query.Params.Count == 0;
        var routeParamName = isSingleRouteParam ? NamingConventions.ToCamelCase(query.Params[0].Name) : null;

        var dispatchCall = isSingleRouteParam
            ? $"{DispatcherParam}.Send(new {messageName}({routeParamName}), cancellationToken)"
            : isZeroParam
                ? $"{DispatcherParam}.Send(new {messageName}(), cancellationToken)"
                : $"{DispatcherParam}.Send(query, cancellationToken)";

        var endpointModel = new QueryEndpointRenderModel
        {
            Namespace = ns,
            RootNamespace = context.RootNamespace,
            MessageName = messageName,
            EndpointName = endpointName,
            ResultType = resultType,
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
        };

        return new[]
        {
            new GeneratedFile($"{slicePath}/{messageName}.cs", RenderAdapter("Message.cs.sbn", messageModel)),
            new GeneratedFile($"{slicePath}/{handlerName}.cs", RenderAdapter("Handler.cs.sbn", messageModel)),
            new GeneratedFile($"{slicePath}/{endpointName}.cs", RenderShared("QueryEndpoint.cs.sbn", endpointModel)),
        };
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
