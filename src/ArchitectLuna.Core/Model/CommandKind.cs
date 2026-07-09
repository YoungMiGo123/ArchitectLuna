namespace ArchitectLuna.Core.Model;

/// <summary>
/// Drives both the inferred HTTP verb/route shape and the endpoint's parameter binding:
/// Create is a plain POST with a JSON body; Update is a PUT to ".../{id}" that merges the route
/// id into the body; Delete is a DELETE to ".../{id}" with no body and no validator.
/// </summary>
public enum CommandKind
{
    Create,
    Update,
    Delete,
}
