using ArchitectLuna.Core.Model;

namespace ArchitectLuna.Core.Naming;

/// <summary>
/// Default route inference, shared by every adapter so switching --adapter never changes an
/// API's route shape, only its implementation.
/// </summary>
public static class RouteInference
{
    public static string CommandRoute(FeatureModel feature, CommandModel command)
    {
        if (command.Route is not null)
        {
            return command.Route;
        }

        var featureSegment = NamingConventions.ToKebabCase(feature.Name);
        return command.Kind is CommandKind.Update or CommandKind.Delete
            ? $"/api/{featureSegment}/{{id}}"
            : $"/api/{featureSegment}";
    }

    public static string QueryRoute(FeatureModel feature, QueryModel query)
    {
        if (query.Route is not null)
        {
            return query.Route;
        }

        var featureSegment = NamingConventions.ToKebabCase(feature.Name);

        if (query.Params.Count == 0)
        {
            return $"/api/{featureSegment}";
        }

        if (query.Params.Count == 1 && query.Params[0].Name.EndsWith("Id", StringComparison.Ordinal))
        {
            var routeParam = NamingConventions.ToCamelCase(query.Params[0].Name);
            return $"/api/{featureSegment}/{{{routeParam}}}";
        }

        return $"/api/{featureSegment}/{NamingConventions.ToKebabCase(query.Name)}";
    }
}
