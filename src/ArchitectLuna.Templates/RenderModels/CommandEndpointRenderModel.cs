namespace ArchitectLuna.Templates.RenderModels;

/// <summary>
/// Rendered by the single shared CommandEndpoint.cs.sbn template for every adapter — the
/// endpoint always maps a minimal-API route through <c>IEndpointDefinition</c>; only the
/// dispatcher fields and the Create/Update/Delete shape vary.
/// </summary>
public sealed class CommandEndpointRenderModel
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

    /// <summary>Where the Result pattern types live — the dispatch expression can name Result&lt;T&gt; explicitly (Wolverine's InvokeAsync).</summary>
    public required string ResultsNamespace { get; init; }

    /// <summary>The Request DTO bound from the JSON body; null when <see cref="HasBody"/> is false (Delete).</summary>
    public string? RequestName { get; init; }

    /// <summary>Where the Request/Response DTOs live (the Contracts target's slice namespace).</summary>
    public string? ContractsNamespace { get; init; }

    /// <summary>True when the endpoint references Request/Response types from a different namespace (Clean Architecture Contracts project).</summary>
    public required bool HasContractsUsing { get; init; }

    /// <summary>
    /// The expression returned when the dispatched Result succeeds — precomputed per operation so
    /// status-code policy (201 Create / 200 Update / 204 Delete) lives in adapter code, not the template.
    /// </summary>
    public required string SuccessExpression { get; init; }
}
