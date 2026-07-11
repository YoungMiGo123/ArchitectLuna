namespace ArchitectLuna.Core.Model;

/// <summary>
/// How generated HTTP endpoints are hosted. Orthogonal to <see cref="ArchitectModel.Adapter"/>
/// and <see cref="ArchitectModel.Persistence"/>: either style can pair with any adapter/provider,
/// and both produce the identical HTTP surface (same routes, verbs, and
/// <c>ApiResponse&lt;T&gt;</c> envelope) — only the hosting mechanism differs.
/// </summary>
public enum ApiStyle
{
    /// <summary>
    /// ASP.NET Core Minimal API: each operation is an <c>IEndpointDefinition</c> discovered and
    /// mapped by <c>MapApiEndpoints()</c>. The default for `new api`.
    /// </summary>
    MinimalApi,

    /// <summary>
    /// Traditional MVC-style controllers: each operation is a single-action
    /// <c>[ApiController]</c> class discovered by ASP.NET's controller routing.
    /// </summary>
    Controllers,
}
