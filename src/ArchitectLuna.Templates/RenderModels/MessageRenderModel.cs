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
}
