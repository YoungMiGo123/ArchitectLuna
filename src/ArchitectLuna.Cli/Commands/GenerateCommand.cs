using System.ComponentModel;
using ArchitectLuna.Cli.Scaffolding;
using Spectre.Console.Cli;

namespace ArchitectLuna.Cli.Commands;

public sealed class GenerateCommandSettings : CommandSettings
{
    [CommandOption("--no-format")]
    [Description("Skip running 'dotnet format' over the solution after generation.")]
    [DefaultValue(false)]
    public bool NoFormat { get; init; }
}

public sealed class GenerateCommand : Command<GenerateCommandSettings>
{
    protected override int Execute(CommandContext context, GenerateCommandSettings settings, CancellationToken cancellationToken)
    {
        if (!WorkspaceGuard.TryLocateModelPath(out var modelPath))
        {
            return 1;
        }

        var root = Path.GetDirectoryName(Path.GetDirectoryName(modelPath))!;
        return GenerationRunner.Run(root, modelPath, format: !settings.NoFormat) ? 0 : 1;
    }
}
