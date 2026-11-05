using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Core.Agent.Tools.Shell;

/// <summary>
/// Runs an <c>az agent tool ...</c> subprocess and returns its stdout. Every
/// agent-side operation that needs a CLI subprocess funnels through here.
/// Help uses it only for catalog regeneration; warm help and environment reads
/// run in process.
/// </summary>
public static class ShellOutHelper
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>
    /// Invoke <paramref name="azExePath"/> with the given args and return stdout.
    /// On non-zero exit, returns a structured JSON error the LLM can read
    /// instead of throwing — the tool contract is "always return a JSON string".
    /// </summary>
    public static async Task<string> InvokeAzAsync(
        string azExePath,
        IReadOnlyList<string> args,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(azExePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            // az emits UTF-8; force UTF-8 decoding so non-ASCII output (resource
            // names, localized errors, paths) isn't mangled by the OS default
            // code page (e.g. GBK / cp1252) before it's returned to the model as
            // JSON. Matches BicepCompiler's subprocess handling.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var proc = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        try
        {
            proc.Start();
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = "shell_out_spawn_failed",
                azExePath,
                message = ex.Message,
            }, Json);
        }

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(DefaultTimeout);
        try
        {
            await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { /* best effort */ }
            return JsonSerializer.Serialize(new
            {
                error = "shell_out_timeout",
                timeoutSeconds = (int)DefaultTimeout.TotalSeconds,
                argv = args,
            }, Json);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Caller-driven cancellation (user Ctrl+C, host shutdown, tool
            // budget exhausted, ...). Same contract as the timeout path: kill
            // the still-running child and return a structured JSON error so
            // "always return a JSON string" holds regardless of who cancels.
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { /* best effort */ }
            return JsonSerializer.Serialize(new
            {
                error = "shell_out_cancelled",
                argv = args,
            }, Json);
        }

        // WaitForExitAsync can return before OutputDataReceived / ErrorDataReceived
        // have delivered the last lines — Process on .NET signals exit as soon as
        // the child ends, but the async event pumps run separately and may still
        // be draining the pipes. A trailing sync WaitForExit() joins those pumps,
        // guaranteeing stdout/stderr are fully populated before we serialize.
        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            return JsonSerializer.Serialize(new
            {
                error = "shell_out_nonzero_exit",
                exitCode = proc.ExitCode,
                argv = args,
                stderr = stderr.ToString().TrimEnd(),
                stdout = stdout.ToString().TrimEnd(),
            }, Json);
        }

        return stdout.ToString();
    }
}
