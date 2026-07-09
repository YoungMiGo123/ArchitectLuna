namespace ArchitectLuna.Templates.RenderModels;

/// <summary>
/// Rendered by the single shared CommandEndpoint.cs.sbn template for every adapter — the
/// endpoint always maps a minimal-API route through <c>IEndpointDefinition</c>; only the
/// dispatcher fields and the Create/Update/Delete shape vary.
/// </summary>
public sealed class CommandEndpointRenderModel
{
    public required string Namespace { get; init; }

    public required string RootNamespace { get; init; }

    public required string MessageName { get; init; }

    public required string EndpointName { get; init; }

    public required string ResultType { get; init; }

    public required string Route { get; init; }

    /// <summary>Minimal-API mapping method: "MapPost", "MapPut", or "MapDelete".</summary>
    public required string HttpMapMethod { get; init; }

    /// <summary>True for Update/Delete: the route carries a "{id}" segment bound as a Guid.</summary>
    public required bool HasRouteId { get; init; }

    /// <summary>True for Create/Update: the request carries a JSON body bound and validated.</summary>
    public required bool HasBody { get; init; }

    /// <summary>Namespace to "using" for the dispatcher type (e.g. "MediatR", "Wolverine").</summary>
    public required string DispatcherUsing { get; init; }

    /// <summary>Dispatcher interface injected into the endpoint delegate (e.g. "ISender", "IMessageBus").</summary>
    public required string DispatcherType { get; init; }

    /// <summary>Parameter name for the injected dispatcher (e.g. "sender", "bus").</summary>
    public required string DispatcherParam { get; init; }

    /// <summary>Full await-able dispatch expression, e.g. "sender.Send(command, cancellationToken)".</summary>
    public required string DispatchCall { get; init; }
}
