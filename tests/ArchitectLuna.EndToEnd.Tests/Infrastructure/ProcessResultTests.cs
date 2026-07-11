using Xunit;

namespace ArchitectLuna.EndToEnd.Tests.Infrastructure;

/// <summary>
/// Pure-function coverage for <see cref="ProcessResult.CombinedOutputNormalized"/> — no process
/// spawning, so this runs fast and doesn't need the "EndToEnd" trait. Exists because a real CI
/// failure showed Spectre.Console re-emitting a color escape sequence at a word-wrap boundary
/// (e.g. "\x1b[0m\x1b[38;5;9m") splitting a phrase like "Create the entity first" into two lines
/// — collapsing whitespace alone wasn't enough to reassemble it.
/// </summary>
public sealed class ProcessResultTests
{
    [Fact]
    public void CombinedOutputNormalized_StripsAnsiEscapeSequenceAtALineWrapBoundary()
    {
        // Exactly how Spectre.Console wrapped this in the CI run that exposed the bug: the
        // colored line breaks mid-phrase, closing then reopening the color escape sequence.
        const string wrapped =
            "\x1b[38;5;9mEntity 'PaymentRequest' does not exist in feature 'Payments'. Create the entity \x1b[0m\n" +
            "\x1b[38;5;9mfirst: architect-luna add entity Payments PaymentRequest --field Name:Type\x1b[0m\n";

        var result = new ProcessResult(1, wrapped, string.Empty);

        Assert.Contains("Create the entity first", result.CombinedOutputNormalized());
    }

    [Fact]
    public void CombinedOutputNormalized_CollapsesPlainLineBreaksAndSpaces()
    {
        const string wrapped = "Created feature 'Payments'.\n  \nSome   other   line\n";

        var result = new ProcessResult(0, wrapped, string.Empty);

        Assert.Equal("Created feature 'Payments'. Some other line", result.CombinedOutputNormalized());
    }

    [Fact]
    public void CombinedOutputNormalized_CombinesStdoutAndStderr()
    {
        var result = new ProcessResult(1, "out text", "err text");

        var combined = result.CombinedOutputNormalized();

        Assert.Contains("out text", combined);
        Assert.Contains("err text", combined);
    }
}
