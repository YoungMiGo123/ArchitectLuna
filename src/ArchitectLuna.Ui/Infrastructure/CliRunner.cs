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
            RedirectStandardInput = true,
            UseShellExecute = false,
        };

        // See SolutionScaffolder.RunDotnet's doc comment: forces a fresh MSBuild process instead
        // of risking a hang reusing a stale/orphaned node, for any command this ends up running
        // (architect-luna itself shells out to `dotnet` for `new api`).
        psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";

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

        // Close stdin immediately so a child that unexpectedly tries to read from it (an
        // interactive NuGet prompt, say) gets EOF instead of blocking forever — see
        // SolutionScaffolder.RunDotnet's doc comment for the concrete case this fixed.
        process.StandardInput.Close();

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
