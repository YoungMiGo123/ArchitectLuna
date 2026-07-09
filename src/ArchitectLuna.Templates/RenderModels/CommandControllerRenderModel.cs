namespace ArchitectLuna.Templates.RenderModels;

/// <summary>
/// Rendered by the shared CommandController.cs.sbn template for every adapter — the sibling of
/// <see cref="CommandEndpointRenderModel"/> for <c>--api-style controllers</c>: one
/// <c>[ApiController]</c> class with a single "Handle" action per operation, discovered by
/// ASP.NET's controller routing instead of <c>IEndpointDefinition</c>. The route string is used
/// verbatim (as an absolute attribute route, e.g. <c>[HttpPost("/api/invoices")]</c>) so the URL
/// surface is byte-for-byte identical to the Minimal API style regardless of any custom
/// <c>CommandModel.Route</c> override.
/// </summary>
public sealed class CommandControllerRenderModel
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

    /// <summary>The controller class name, e.g. "CreateInvoiceController".</summary>
    public required string ControllerName { get; init; }

    public required string ResultType { get; init; }

    /// <summary>Full route, e.g. "/api/invoices" or "/api/invoices/{id}", used as an absolute attribute route.</summary>
    public required string Route { get; init; }

    /// <summary>Attribute name: "HttpPost", "HttpPut", or "HttpDelete".</summary>
    public required string HttpAttribute { get; init; }

    /// <summary>True for Update/Delete: the route carries a "{id}" segment bound as a Guid.</summary>
    public required bool HasRouteId { get; init; }

    /// <summary>True for Create/Update: the request carries a JSON body bound and validated.</summary>
    public required bool HasBody { get; init; }

    /// <summary>Namespace to "using" for the dispatcher type (e.g. "MediatR", "Wolverine").</summary>
    public required string DispatcherUsing { get; init; }

    /// <summary>Dispatcher interface injected into the action (e.g. "ISender", "IMessageBus").</summary>
    public required string DispatcherType { get; init; }

    /// <summary>Parameter name for the injected dispatcher (e.g. "sender", "bus").</summary>
    public required string DispatcherParam { get; init; }

    /// <summary>Full await-able dispatch expression, e.g. "sender.Send(command, cancellationToken)".</summary>
    public required string DispatchCall { get; init; }

    /// <summary>Where the Result pattern types live.</summary>
    public required string ResultsNamespace { get; init; }

    /// <summary>The Request DTO bound from the JSON body; null when <see cref="HasBody"/> is false (Delete).</summary>
    public string? RequestName { get; init; }

    /// <summary>Where the Request/Response DTOs live (the Contracts target's slice namespace).</summary>
    public string? ContractsNamespace { get; init; }

    /// <summary>True when the action references Request/Response types from a different namespace (Clean Architecture Contracts project).</summary>
    public required bool HasContractsUsing { get; init; }

    /// <summary>
    /// The expression returned when the dispatched Result succeeds or fails — a single
    /// <c>ResultActionExtensions</c> call (<c>ToCreatedActionResponse</c>/<c>ToOkActionResponse</c>/
    /// <c>ToNoContentActionResponse</c>) returning <c>IActionResult</c>.
    /// </summary>
    public required string SuccessExpression { get; init; }

    /// <summary>The response DTO type named in the success ProducesResponseType attribute; null for Delete (204, no body).</summary>
    public string? SuccessResponseType { get; init; }

    /// <summary>The success HTTP status code constant, e.g. "StatusCodes.Status201Created".</summary>
    public required string SuccessStatusCode { get; init; }
}
