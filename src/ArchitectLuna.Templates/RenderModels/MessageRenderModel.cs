namespace ArchitectLuna.Templates.RenderModels;

/// <summary>
/// Shared by both the Command/Query record file and the Handler file — the two adapters render
/// this same shape through their own .sbn templates.
/// </summary>
public sealed class MessageRenderModel
{
    public required string Namespace { get; init; }

    public required string RootNamespace { get; init; }

    public required string MessageName { get; init; }

    public required string HandlerName { get; init; }

    public required string ResultName { get; init; }

    /// <summary>
    /// The type used everywhere the result is referenced as a type argument or return type
    /// (IRequest&lt;T&gt;, Task&lt;T&gt;). Equal to <see cref="ResultName"/> normally, or
    /// "IReadOnlyList&lt;{ResultName}&gt;" for a collection query — <see cref="ResultName"/>
    /// itself always names the single-item record actually declared in this file.
    /// </summary>
    public required string ResultType { get; init; }

    public required IReadOnlyList<MessageFieldRenderModel> Fields { get; init; }

    public required IReadOnlyList<MessageFieldRenderModel> ResultFields { get; init; }

    /// <summary>
    /// Initial content of the handler's protected region — "throw new NotImplementedException();"
    /// when no persistence provider is configured (or this message has no entity link), or real
    /// CRUD statements otherwise. Only used by Handler.cs.sbn; ignored by Message.cs.sbn.
    /// </summary>
    public required string HandlerBody { get; init; }

    /// <summary>Extra using directives the handler body needs (e.g. "Microsoft.EntityFrameworkCore").</summary>
    public required IReadOnlyList<string> HandlerUsings { get; init; }

    /// <summary>
    /// True when the handler needs one dependency injected (a DbContext, an IDocumentSession,
    /// etc.) — MediatR gets it via constructor injection, Wolverine via an extra static-method
    /// parameter (Wolverine's own convention). Null/false means a parameterless handler.
    /// </summary>
    public bool HasHandlerDependency { get; init; }

    public string? HandlerDependencyType { get; init; }

    public string? HandlerDependencyParam { get; init; }
}
