namespace ArchitectLuna.Templates.RenderModels;

/// <summary>
/// Rendered by the shared Mappings.cs.sbn template: the explicit extension-method mapping layer
/// between the HTTP contract (Request/Response DTOs) and the application messages
/// (Command/Query + Result). Argument lists are precomputed strings so the template stays dumb.
/// </summary>
public sealed class MappingsRenderModel
{
    /// <summary>The Application slice namespace the mappings class lives in.</summary>
    public required string Namespace { get; init; }

    /// <summary>Where the Request/Response DTOs live (the Contracts target's slice namespace).</summary>
    public required string ContractsNamespace { get; init; }

    /// <summary>False under vertical slice, where DTOs share the slice namespace and a using would be redundant.</summary>
    public required bool NeedsContractsUsing { get; init; }

    public required string MappingsName { get; init; }

    public required bool HasRequest { get; init; }

    public string? RequestName { get; init; }

    public string? MessageName { get; init; }

    /// <summary>True for Update-shaped commands: the id binds from the route, not the request body.</summary>
    public bool RequestTakesId { get; init; }

    /// <summary>Constructor arguments for the message, in the message record's positional order (e.g. "id, request.Name").</summary>
    public string? ToCommandArgs { get; init; }

    public required bool HasResponse { get; init; }

    public string? ResultName { get; init; }

    public string? ResponseName { get; init; }

    /// <summary>Constructor arguments for the response record (e.g. "result.Id, result.Name").</summary>
    public string? ToResponseArgs { get; init; }
}
