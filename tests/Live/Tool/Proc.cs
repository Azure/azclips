using System.Diagnostics;
using System.Text;

namespace Live.Tool;

/// <summary>Result of running an external command.</summary>
internal sealed record ProcResult(int ExitCode, string Stdout, string Stderr)
{
    public bool Ok => ExitCode == 0;

    /// <summary>Stdout trimmed of whitespace and any trailing CR.</summary>
    public string Out => Stdout.Trim();
}

/// <summary>
/// Runs the external tools this port still shells out to: <c>az</c>, <c>terraform</c>
/// and <c>dotnet</c>. None of them has a usable .NET API, so the subprocess is not
/// something the port removes — it is something the port has to get right.
/// </summary>
internal static class Proc
{
    /// <summary>
    /// Run a command and capture its output.
    /// <para>
    /// stdout and stderr are drained on <b>separate threads</b> while the process runs.
    /// Reading them sequentially deadlocks as soon as either exceeds its pipe buffer
    /// (~64KB) — <c>terraform init</c> and <c>dotnet publish</c> both do. This is the
    /// same class of bug as a child inheriting a pipe it never closes, which cost every
    /// captured <c>az</c> call ~4.5s until it was fixed in Core.Cli.
    /// </para>
    /// </summary>
    public static ProcResult Run(
        string file,
        IEnumerable<string> args,
        string? workingDirectory = null,
        IDictionary<string, string>? env = null,
        bool echo = false,
        bool passthrough = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ResolveExecutable(file),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = !passthrough,
            RedirectStandardError = !passthrough,
            // Always redirect stdin and close it: a tool that decides to prompt
            // (terraform without -input=false, az on a token refresh) would
            // otherwise block forever against an inherited console.
            RedirectStandardInput = true,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
        };

        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        if (env is not null)
        {
            foreach (var (k, v) in env)
            {
                psi.Environment[k] = v;
            }
        }

        if (echo)
        {
            Console.WriteLine($"    $ {file} {string.Join(' ', args)}");
        }

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start '{file}'.");

        p.StandardInput.Close();

        if (passthrough)
        {
            p.WaitForExit();
            return new ProcResult(p.ExitCode, string.Empty, string.Empty);
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var outDone = ReadAsync(p.StandardOutput, stdout);
        var errDone = ReadAsync(p.StandardError, stderr);

        p.WaitForExit();
        outDone.Wait();
        errDone.Wait();

        return new ProcResult(p.ExitCode, Clean(stdout.ToString()), Clean(stderr.ToString()));
    }

    /// <summary>Run a command, streaming its output straight to the console.</summary>
    public static ProcResult RunStreaming(
        string file,
        IEnumerable<string> args,
        string? workingDirectory = null,
        IDictionary<string, string>? env = null)
        => Run(file, args, workingDirectory, env, echo: false, passthrough: true);

    /// <summary>Run a command and throw with its stderr when it fails.</summary>
    public static string Check(
        string file,
        IEnumerable<string> args,
        string? workingDirectory = null,
        IDictionary<string, string>? env = null)
    {
        var r = Run(file, args, workingDirectory, env);
        if (!r.Ok)
        {
            var detail = string.IsNullOrWhiteSpace(r.Stderr) ? r.Stdout : r.Stderr;
            throw new LiveToolException($"{file} {string.Join(' ', args)} failed (exit {r.ExitCode}).\n{detail.Trim()}");
        }

        return r.Out;
    }

    private static Task ReadAsync(StreamReader reader, StringBuilder sink) =>
        Task.Run(async () =>
        {
            var buffer = new char[4096];
            int n;
            while ((n = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                lock (sink)
                {
                    sink.Append(buffer, 0, n);
                }
            }
        });

    /// <summary>
    /// Strip carriage returns. On Windows — and on WSL, where <c>az</c> often resolves
    /// to the Windows CLI through interop — output is CRLF, and a stray trailing CR
    /// silently corrupts SAS tokens, storage keys and resource IDs.
    /// </summary>
    private static string Clean(string s) => s.Replace("\r", string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// Find the real executable for a bare command name.
    /// <para>
    /// Needed because <c>az</c> on Windows is <c>az.cmd</c>, and process creation with
    /// <c>UseShellExecute=false</c> does not apply PATHEXT. Without this the tool would
    /// work everywhere except the platform it was written to support.
    /// </para>
    /// </summary>
    public static string ResolveExecutable(string command)
    {
        if (command.Contains(Path.DirectorySeparatorChar) || command.Contains('/'))
        {
            return command;
        }

        if (!OperatingSystem.IsWindows())
        {
            return command;
        }

        var exts = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var dir in paths)
        {
            foreach (var ext in exts)
            {
                var candidate = Path.Combine(dir.Trim('"'), command + ext);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return command;
    }

    /// <summary>True when the command can be found on PATH.</summary>
    public static bool Exists(string command)
    {
        try
        {
            var probe = command == "terraform" ? "-version" : "--version";
            var r = Run(command, [probe]);
            return r.ExitCode is 0 or 1;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>An error already explained to the user; Main prints it without a stack trace.</summary>
internal sealed class LiveToolException(string message) : Exception(message);
