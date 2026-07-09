using System.Text;
using System.Text.RegularExpressions;

namespace ArchitectLuna.Core.Naming;

public static partial class NamingConventions
{
    public static string ToPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var words = SplitWords(value);
        var builder = new StringBuilder();
        foreach (var word in words)
        {
            builder.Append(char.ToUpperInvariant(word[0]));
            if (word.Length > 1)
            {
                builder.Append(word[1..]);
            }
        }

        return builder.ToString();
    }

    public static string ToCamelCase(string value)
    {
        var pascal = ToPascalCase(value);
        if (pascal.Length == 0)
        {
            return pascal;
        }

        return char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    public static string ToKebabCase(string value) => string.Join('-', SplitWords(value).Select(w => w.ToLowerInvariant()));

    /// <summary>
    /// Naive English pluralization for entity/feature names (e.g. "Invoice" -&gt; "Invoices",
    /// "Category" -&gt; "Categories"). Good enough for generated route/query names; irregular
    /// plurals are not handled.
    /// </summary>
    public static string Pluralize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (value.Length > 1 && value.EndsWith('y') && !IsVowel(value[^2]))
        {
            return value[..^1] + "ies";
        }

        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("z", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("sh", StringComparison.OrdinalIgnoreCase))
        {
            return value + "es";
        }

        return value + "s";
    }

    private static bool IsVowel(char c) => "aeiouAEIOU".IndexOf(c) >= 0;

    public static string ToSnakeCase(string value) => string.Join('_', SplitWords(value).Select(w => w.ToLowerInvariant()));

    private static IReadOnlyList<string> SplitWords(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Array.Empty<string>();
        }

        var normalized = WordBoundaryPattern().Replace(value, " ");
        return normalized
            .Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
    }

    [GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex WordBoundaryPattern();
}
