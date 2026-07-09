namespace ArchitectLuna.Templates.RenderModels;

/// <summary>
/// Rendered by the single shared QueryEndpoint.cs.sbn template for every adapter — the endpoint
/// always maps a minimal-API GET through <c>IEndpointDefinition</c>; only the dispatcher fields
/// vary per adapter (e.g. MediatR's ISender vs Wolverine's IMessageBus).
/// </summary>
public sealed class QueryEndpointRenderModel
{
    public required string Namespace { get; init; }

    /// <summary>The Api project's root namespace — IEndpointDefinition always lives here.</summary>
    public required string ApiRootNamespace { get; init; }

    /// <summary>
    /// The message/result types' namespace. Always "using"-ed, even when it equals
    /// <see cref="Namespace"/> (vertical slice — a same-namespace using is legal, harmless, and
    /// keeps this template identical across layouts instead of branching on one).
    /// </summary>
    public required string MessageNamespace { get; init; }

    public required string MessageName { get; init; }

    public required string EndpointName { get; init; }

    public required string ResultType { get; init; }

    public required string Route { get; init; }

    public required IReadOnlyList<MessageFieldRenderModel> Params { get; init; }

    public required bool IsSingleRouteParam { get; init; }

    /// <summary>True for a zero-param query (e.g. GetAll): no message parameter is bound at all.</summary>
    public required bool IsZeroParam { get; init; }

    public string? RouteParamName { get; init; }

    public string? RouteParamType { get; init; }

    /// <summary>Namespace to "using" for the dispatcher type (e.g. "MediatR", "Wolverine").</summary>
    public required string DispatcherUsing { get; init; }

    /// <summary>Dispatcher interface injected into the endpoint delegate (e.g. "ISender", "IMessageBus").</summary>
    public required string DispatcherType { get; init; }

    /// <summary>Parameter name for the injected dispatcher (e.g. "sender", "bus").</summary>
    public required string DispatcherParam { get; init; }

    /// <summary>Full await-able dispatch expression, e.g. "sender.Send(query, cancellationToken)".</summary>
    public required string DispatchCall { get; init; }
}
