using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace ArchitectLuna.EndToEnd.Tests.Infrastructure;

/// <summary>Result of a shelled-out process run: exit code plus captured stdout/stderr for assertion failure messages.</summary>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public override string ToString() =>
        $"exit code {ExitCode}\n--- stdout ---\n{StandardOutput}\n--- stderr ---\n{StandardError}";

    /// <summary>
    /// Stdout+stderr with all whitespace runs (including line breaks) collapsed to a single
    /// space. Spectre.Console word-wraps markup output to the detected console width, which
    /// differs by environment (e.g. this sandbox vs. a GitHub Actions runner) — a multi-word
    /// phrase assertion against the raw output can flake if a line break happens to fall inside
    /// the exact phrase being checked. Assert against this instead of the raw strings whenever a
    /// check spans more than one word.
    /// </summary>
    public string CombinedOutputNormalized() =>
        Regex.Replace(StandardOutput + " " + StandardError, @"\s+", " ").Trim();
}

/// <summary>Shells out to a process (the built CLI, or `dotnet build`) and captures its result.</summary>
public static class ProcessRunner
{
    // Generous because a Clean Architecture scaffold shells out to ~20 sequential
    // `dotnet add package` calls, each of which round-trips to the NuGet feed even when the
    // package is already in the local cache.
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    public static ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory, TimeSpan? timeout = null)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process '{fileName}'.");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var effectiveTimeout = timeout ?? DefaultTimeout;
        if (!process.WaitForExit((int)effectiveTimeout.TotalMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // best effort
            }

            throw new TimeoutException(
                $"Process '{fileName} {string.Join(' ', arguments)}' in '{workingDirectory}' did not exit within {effectiveTimeout}.\n" +
                $"--- stdout so far ---\n{stdout}\n--- stderr so far ---\n{stderr}");
        }

        // Ensure async output/error handlers have flushed everything before we read the buffers.
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    /// <summary>Convenience for invoking the architect-luna CLI dll via `dotnet &lt;dll&gt; &lt;args&gt;`.</summary>
    public static ProcessResult RunCli(string cliDllPath, string workingDirectory, params string[] arguments)
    {
        var fullArgs = new List<string> { cliDllPath };
        fullArgs.AddRange(arguments);
        return Run("dotnet", fullArgs, workingDirectory);
    }

    /// <summary>Convenience for `dotnet build` in a given directory.</summary>
    public static ProcessResult RunDotnetBuild(string workingDirectory, TimeSpan? timeout = null) =>
        Run("dotnet", new[] { "build" }, workingDirectory, timeout);
}
