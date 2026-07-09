using ArchitectLuna.Core.Model;
using Xunit;

namespace ArchitectLuna.Core.Tests.Model;

public sealed class DatabaseApplyModeParserTests
{
    [Theory]
    [InlineData("manual", DatabaseApplyMode.Manual)]
    [InlineData("on-generate", DatabaseApplyMode.OnGenerate)]
    [InlineData("on-startup", DatabaseApplyMode.OnStartup)]
    public void Parse_KnownValues_RoundTripsThroughToKebabCase(string kebab, DatabaseApplyMode expected)
    {
        var parsed = DatabaseApplyModeParser.Parse(kebab);

        Assert.Equal(expected, parsed);
        Assert.Equal(kebab, DatabaseApplyModeParser.ToKebabCase(parsed));
    }

    [Fact]
    public void Parse_UnknownValue_ThrowsWithValidValuesListed()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseApplyModeParser.Parse("sometimes"));

        Assert.Contains("manual", ex.Message);
        Assert.Contains("on-generate", ex.Message);
        Assert.Contains("on-startup", ex.Message);
    }
}
