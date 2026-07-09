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

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new CliRunResult(true, process.ExitCode, stdout, stderr);
    }
}
