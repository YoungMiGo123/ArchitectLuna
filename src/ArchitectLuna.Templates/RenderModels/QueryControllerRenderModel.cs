namespace ArchitectLuna.Templates.RenderModels;

/// <summary>
/// Rendered by the shared QueryController.cs.sbn template for every adapter — the sibling of
/// <see cref="QueryEndpointRenderModel"/> for <c>--api-style controllers</c>: one
/// <c>[ApiController]</c> class with a single "Handle" GET action per operation. The route string
/// is used verbatim (as an absolute attribute route, e.g. <c>[HttpGet("/api/invoices/{id}")]</c>)
/// so the URL surface is byte-for-byte identical to the Minimal API style.
/// </summary>
public sealed class QueryControllerRenderModel
{
    public required string Namespace { get; init; }

    /// <summary>The Api project's root namespace.</summary>
    public required string ApiRootNamespace { get; init; }

    /// <summary>
    /// The message/result types' namespace. Always "using"-ed, even when it equals
    /// <see cref="Namespace"/> (vertical slice).
    /// </summary>
    public required string MessageNamespace { get; init; }

    public required string MessageName { get; init; }

    /// <summary>The controller class name, e.g. "GetInvoiceByIdController".</summary>
    public required string ControllerName { get; init; }

    public required string ResultType { get; init; }

    /// <summary>Full route, used as an absolute attribute route.</summary>
    public required string Route { get; init; }

    public required IReadOnlyList<MessageFieldRenderModel> Params { get; init; }

    public required bool IsSingleRouteParam { get; init; }

    /// <summary>True for a zero-param query (e.g. GetAll): no message parameter is bound at all.</summary>
    public required bool IsZeroParam { get; init; }

    public string? RouteParamName { get; init; }

    public string? RouteParamType { get; init; }

    /// <summary>Namespace to "using" for the dispatcher type (e.g. "MediatR", "Wolverine").</summary>
    public required string DispatcherUsing { get; init; }

    /// <summary>Dispatcher interface injected into the action (e.g. "ISender", "IMessageBus").</summary>
    public required string DispatcherType { get; init; }

    /// <summary>Parameter name for the injected dispatcher (e.g. "sender", "bus").</summary>
    public required string DispatcherParam { get; init; }

    /// <summary>Full await-able dispatch expression, e.g. "sender.Send(query, cancellationToken)".</summary>
    public required string DispatchCall { get; init; }

    /// <summary>Where the Result pattern types live.</summary>
    public required string ResultsNamespace { get; init; }

    /// <summary>Where the Response DTO lives (the Contracts target's slice namespace).</summary>
    public string? ContractsNamespace { get; init; }

    /// <summary>True when the action references the Response type from a different namespace (Clean Architecture Contracts project).</summary>
    public required bool HasContractsUsing { get; init; }

    /// <summary>
    /// The expression returned when the dispatched Result succeeds or fails — a single
    /// <c>ResultActionExtensions.ToOkActionResponse</c> call returning <c>IActionResult</c>.
    /// </summary>
    public required string SuccessExpression { get; init; }

    /// <summary>The response DTO/collection/paged type named in the success ProducesResponseType attribute.</summary>
    public required string SuccessResponseType { get; init; }
}
