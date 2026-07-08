using ArchitectLuna.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

// Some hosting environments report an invalid (negative) terminal width, which makes Spectre's
// renderer silently emit nothing instead of falling back to a sane default. Force a width so
// output (including --help) is never silently swallowed.
if (AnsiConsole.Profile.Width <= 0)
{
    AnsiConsole.Profile.Width = 120;
}

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("architect-luna");

    config.AddBranch("new", newBranch =>
    {
        newBranch.AddCommand<NewApiCommand>("api");
    });

    config.AddBranch("add", addBranch =>
    {
        addBranch.AddCommand<AddFeatureCommand>("feature");
        addBranch.AddCommand<AddEntityCommand>("entity");
        addBranch.AddCommand<AddCommandCommand>("command");
        addBranch.AddCommand<AddQueryCommand>("query");
    });

    config.AddCommand<GenerateCommand>("generate");
});

return app.Run(args);
