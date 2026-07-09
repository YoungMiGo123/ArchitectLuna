namespace ArchitectLuna.Core.Generation;

/// <summary>
/// Everything a messaging adapter needs to splice a persistence provider's logic into a
/// generated handler: the statements themselves, the one dependency the handler needs injected
/// (a DbContext, an `IDocumentSession`, etc.), and any extra usings.
///
/// Exactly one dependency is supported by design — MediatR handlers get it constructor-injected,
/// Wolverine handlers get it as an extra static-method parameter (Wolverine's own convention);
/// both need "one thing to plumb through," not an arbitrary DI graph, to keep generated code
/// simple. A provider needing more than that should expose its own composed service instead of
/// asking handlers to take multiple raw dependencies.
/// </summary>
public sealed record HandlerBinding(
    string Body,
    string? DependencyType,
    string? DependencyParam,
    IReadOnlyList<string> Usings)
{
    public static HandlerBinding NotImplemented() =>
        new("throw new NotImplementedException();", null, null, Array.Empty<string>());
}
