using System.Diagnostics;

namespace ArchitectLuna.Ui.Infrastructure;

public sealed record CliRunResult(bool Started, int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Shells out to the built architect-luna CLI, mirroring the <c>Process.Start</c> technique
/// <c>SolutionScaffolder</c> already uses for invoking <c>dotnet</c>.
/// </summary>
public static class CliRunner
{
    public static CliRunResult Run(string cliDllPath, string workingDirectory, params string[] arguments)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        psi.ArgumentList.Add(cliDllPath);
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi);
        if (process is null)
        {
            return new CliRunResult(false, -1, string.Empty, "Failed to start 'dotnet' process.");
        }

        // Async drain via events, not a blocking stdout.ReadToEnd() then stderr.ReadToEnd(): that
        // sequential pattern deadlocks if the child fills the stderr pipe buffer while stdout is
        // still open — see SolutionScaffolder.RunDotnet's doc comment for how this actually bit
        // `new api` under Clean Architecture (multi-minute hangs from `dotnet add package` calls).
        var stdout = new System.Text.StringBuilder();
        var stderr = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return new CliRunResult(true, process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
