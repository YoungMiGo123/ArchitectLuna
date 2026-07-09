using ArchitectLuna.Core.Model;

namespace ArchitectLuna.Core.Validation;

public static class ModelValidator
{
    private static readonly string[] KnownAdapters = { "mediatr", "wolverine" };

    public static ValidationResult Validate(ArchitectModel model)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(model.SolutionName))
        {
            errors.Add("solutionName is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Namespace))
        {
            errors.Add("namespace is required.");
        }

        if (!KnownAdapters.Contains(model.Adapter))
        {
            errors.Add($"adapter must be one of [{string.Join(", ", KnownAdapters)}], was '{model.Adapter}'.");
        }

        if (model.Layout is null)
        {
            errors.Add($"layout is required and must be one of [{string.Join(", ", Enum.GetNames<SolutionLayout>())}].");
        }

        var featureNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var feature in model.Features)
        {
            if (!featureNames.Add(feature.Name))
            {
                errors.Add($"duplicate feature name '{feature.Name}'.");
            }

            ValidateCommands(feature, errors);
            ValidateQueries(feature, errors);
        }

        return new ValidationResult(errors);
    }

    private static void ValidateCommands(FeatureModel feature, List<string> errors)
    {
        var commandNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var command in feature.Commands)
        {
            if (!commandNames.Add(command.Name))
            {
                errors.Add($"duplicate command '{command.Name}' in feature '{feature.Name}'.");
            }

            var fieldNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in command.Fields)
            {
                if (!fieldNames.Add(field.Name))
                {
                    errors.Add($"duplicate field '{field.Name}' on command '{feature.Name}.{command.Name}'.");
                }
            }
        }
    }

    private static void ValidateQueries(FeatureModel feature, List<string> errors)
    {
        var queryNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var query in feature.Queries)
        {
            if (!queryNames.Add(query.Name))
            {
                errors.Add($"duplicate query '{query.Name}' in feature '{feature.Name}'.");
            }

            var paramNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var param in query.Params)
            {
                if (!paramNames.Add(param.Name))
                {
                    errors.Add($"duplicate param '{param.Name}' on query '{feature.Name}.{query.Name}'.");
                }
            }
        }
    }
}
