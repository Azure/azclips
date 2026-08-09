using System.Diagnostics;
using System.Text;

namespace Live.Tests;

/// <summary>Result of one invocation of the bundled <c>az</c> host.</summary>
internal sealed record AzResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;

    /// <summary>Combined output, indented, for use in assertion messages.</summary>
    public string Describe()
    {
        var builder = new StringBuilder();
        builder.Append("exit code ").Append(ExitCode);

        if (!string.IsNullOrWhiteSpace(StandardOutput))
        {
            builder.AppendLine().Append("stdout:").AppendLine().Append(Indent(StandardOutput));
        }

        if (!string.IsNullOrWhiteSpace(StandardError))
        {
            builder.AppendLine().Append("stderr:").AppendLine().Append(Indent(StandardError));
        }

        return builder.ToString();

        static string Indent(string text) =>
            string.Join(
                Environment.NewLine,
                text.TrimEnd().Split('\n').Select(line => "    " + line.TrimEnd('\r')));
    }
}

/// <summary>
/// Thin wrapper around the bundled <c>az</c> executable under test.
/// <para>
/// The live tests run <b>on the VM</b>, so no IMDS endpoint is overridden — the CLI
/// reaches the real 169.254.169.254 service. That is the whole point of the suite:
/// it is the only way to prove the managed-identity code paths work against a real
/// token issuer rather than the mock used by <c>Cli.Tests</c>.
/// </para>
/// </summary>
internal static class AzCli
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    public static async Task<AzResult> RunAsync(params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = LiveTestEnvironment.Az,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (output) { output.AppendLine(e.Data); }
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (error) { error.AppendLine(e.Data); }
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(Timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            throw new TimeoutException(
                $"'az {string.Join(' ', args)}' did not exit within {Timeout.TotalSeconds:N0}s.");
        }

        string capturedOutput, capturedError;
        lock (output) { capturedOutput = output.ToString(); }
        lock (error) { capturedError = error.ToString(); }

        return new AzResult(process.ExitCode, capturedOutput, capturedError);
    }

    /// <summary>
    /// Clears any cached credential so the next login cannot pass on a previous
    /// identity's token. Without this a later selector could silently succeed using
    /// the token acquired by an earlier one, and the test would prove nothing.
    /// </summary>
    public static Task<AzResult> LogoutAsync() => RunAsync("logout");

    public static Task<AzResult> LoginWithIdentityAsync(params string[] selectorArgs) =>
        RunAsync(["login", "--identity", "--subscription-id", LiveTestEnvironment.SubscriptionId, .. selectorArgs]);
}
