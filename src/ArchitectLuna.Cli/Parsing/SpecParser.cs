using ArchitectLuna.Core.Model;

namespace ArchitectLuna.Cli.Parsing;

/// <summary>
/// Parses the "Name:Type" and "Field:RuleExpression" shorthand accepted by the add command/query CLI flags.
/// </summary>
public static class SpecParser
{
    public static FieldModel ParseField(string spec)
    {
        var (name, type) = SplitPair(spec, "--field", "Name:Type", "CustomerId:Guid");
        return new FieldModel { Name = name, Type = type };
    }

    public static ParamModel ParseParam(string spec)
    {
        var (name, type) = SplitPair(spec, "--param", "Name:Type", "Id:Guid");
        return new ParamModel { Name = name, Type = type };
    }

    public static (string FieldName, string Rule) ParseRule(string spec)
    {
        return SplitPair(spec, "--rule", "Field:RuleExpression", "AmountCents:GreaterThan(0)");
    }

    public static CommandKind ParseKind(string spec) => spec.ToLowerInvariant() switch
    {
        "create" => CommandKind.Create,
        "update" => CommandKind.Update,
        "delete" => CommandKind.Delete,
        _ => throw new InvalidOperationException($"Invalid --kind value '{spec}'. Expected one of: create, update, delete."),
    };

    private static (string Left, string Right) SplitPair(string spec, string optionName, string expectedFormat, string example)
    {
        var parts = spec.Split(':', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new InvalidOperationException(
                $"Invalid {optionName} value '{spec}'. Expected format {expectedFormat}, e.g. {example}.");
        }

        return (parts[0], parts[1]);
    }
}
