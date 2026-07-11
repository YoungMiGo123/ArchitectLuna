using ArchitectLuna.Core.Model;

namespace ArchitectLuna.Core.Naming;

/// <summary>
/// Infers sensible default FluentValidation rule fragments from a field's declared type and
/// name, so validators are useful without requiring an explicit `--rule` for every field. Each
/// entry has the same shape <see cref="FieldModel.Rules"/> expects: a full rule-method call
/// including parentheses (e.g. "NotEmpty()"), ready to splice after "RuleFor(x => x.Field)".
///
/// Defaults are additive, never a substitute for explicit rules: callers should render
/// <c>DefaultsFor(field).Concat(field.Rules).Distinct()</c> so hand-authored rules always render
/// after (and alongside, never instead of) the inferred defaults, with an explicit rule that
/// duplicates a default exactly (e.g. an explicit "MaximumLength(3)" on a field named "Currency")
/// collapsing to one `RuleFor` clause instead of repeating.
/// </summary>
public static class DefaultValidationRules
{
    public static IReadOnlyList<string> DefaultsFor(FieldModel field)
    {
        var type = field.Type.Trim();
        var isNullable = type.EndsWith('?');
        var bareType = isNullable ? type[..^1] : type;
        var name = field.Name;

        var rules = new List<string>();

        switch (bareType)
        {
            case "string":
                if (!isNullable)
                {
                    rules.Add("NotEmpty()");
                }

                if (ContainsIgnoreCase(name, "email"))
                {
                    rules.Add("EmailAddress()");
                }

                if (ContainsIgnoreCase(name, "currency"))
                {
                    rules.Add("MaximumLength(3)");
                }

                break;

            case "Guid":
                if (!isNullable)
                {
                    rules.Add("NotEmpty()");
                }

                break;

            case "int":
            case "long":
            case "decimal":
                if (EqualsIgnoreCase(name, "pageSize") || EqualsIgnoreCase(name, "pageNumber"))
                {
                    rules.Add("GreaterThan(0)");
                }

                break;
        }

        return rules;
    }

    private static bool ContainsIgnoreCase(string value, string term) =>
        value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static bool EqualsIgnoreCase(string value, string term) =>
        string.Equals(value, term, StringComparison.OrdinalIgnoreCase);
}
